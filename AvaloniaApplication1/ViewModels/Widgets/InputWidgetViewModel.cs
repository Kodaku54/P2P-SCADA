using System;
using System.Globalization;
using System.Threading.Tasks;
using Avalonia.Media;
using AvaloniaApplication1.Models;
using AvaloniaApplication1.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AvaloniaApplication1.ViewModels.Widgets;

public enum InputMode { Button, Integer, Decimal }

// Віджет вводу: поведінка залежить від типу датчика (Bool: кнопка, Int: ціле число, Float/Double: дробове)
public partial class InputWidgetViewModel : WidgetViewModelBase
{
    private readonly string _deviceId;
    private readonly string _sensorTag;
    private readonly WidgetConfig _widgetConfig;
    private readonly Action _persist;
    private readonly double _writeValue;
    private readonly string _unit;
    private readonly double _low;
    private readonly double _high;

    private string? _message;
    private bool _messageIsError;
    private int _messageVersion;

    public InputMode Mode { get; }
    public bool IsButton => Mode == InputMode.Button;
    public bool IsNumeric => Mode != InputMode.Button;
    
    public override double MinWidgetWidth => IsButton ? 100 : 200;
    public override double MinWidgetHeight => IsButton ? 60 : 90;

    [ObservableProperty] private string _inputText = "";
    [ObservableProperty] private string _currentText = "—";

    public InputWidgetViewModel(string deviceId, SensorConfig sensor, string title, WidgetConfig config, Action persist)
    {
        _deviceId = deviceId;
        _sensorTag = sensor.Tag;
        _widgetConfig = config;
        _persist = persist;
        _unit = config.Unit;
        _writeValue = config.WriteValue ?? 1;
        Title = title;

        Mode = sensor.DataType switch
        {
            "Bool" => InputMode.Button,
            "Float" or "Double" => InputMode.Decimal,
            _ => InputMode.Integer
        };

        // Діапазон типу даних, звужений межами з налаштувань віджета
        var (low, high) = sensor.DataType switch
        {
            "Short" => (-32768.0, 32767.0),
            "UShort" => (0.0, 65535.0),
            "Int" => (-2147483648.0, 2147483647.0),
            "UInt" => (0.0, 4294967295.0),
            "Float" => (-3.4028235e38, 3.4028235e38),
            _ => (double.MinValue, double.MaxValue)
        };
        _low = config.Min.HasValue ? Math.Max(low, config.Min.Value) : low;
        _high = config.Max.HasValue ? Math.Min(high, config.Max.Value) : high;

        // Поле вводу починається з останнього записаного значення
        if (IsNumeric && config.LastValue is { } lastValue)
            InputText = Format(lastValue);
        
        // Стан зв'язку береться з тега датчика, тому датчик має опитуватись
        BindTag(DashboardFactory.TagId(deviceId, sensor.Tag),
            tagValue => CurrentText = $"{tagValue.Value:0.##} {_unit}".Trim());

        PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(IsStale))
                ApplyCommand.NotifyCanExecuteChanged();
        };
    }

    private static string Format(double v) => v.ToString("R", CultureInfo.InvariantCulture);

    private bool CanApply() => !IsStale;

    [RelayCommand(CanExecute = nameof(CanApply))]
    private async Task ApplyAsync()
    {
        double value;
        if (IsButton)
            value = _writeValue;
        else if (!TryReadInput(out value))
            return;

        SetMessage(null);
        var error = await DeviceManager.Default.WriteAsync(_deviceId, _sensorTag, value);

        if (error != null)
        {
            SetMessage(error, true);
            return;
        }

        SetMessage("Записано");
        if (IsNumeric)
        {
            _widgetConfig.LastValue = value;   // запам'ятовуємо лише після успішного запису
            _persist();
        }
    }

    private bool TryReadInput(out double value)
    {
        var text = (InputText ?? "").Trim().Replace(',', '.');

        if (!double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value) || !double.IsFinite(value))
        {
            SetMessage("Введіть число.", true);
            return false;
        }

        if (Mode == InputMode.Integer && value != Math.Floor(value))
        {
            SetMessage("Потрібне ціле число.", true);
            return false;
        }

        if (value < _low || value > _high)
        {
            SetMessage($"Допустимо від {_low:G6} до {_high:G6}.", true);
            return false;
        }

        return true;
    }

    private void SetMessage(string? text, bool isError = false)
    {
        _message = text;
        _messageIsError = isError;
        UpdateStatus();

        var version = ++_messageVersion;

        // Успіх зникає сам, помилка лишається до наступної спроби або зміни тексту
        if (text != null && !isError)
            _ = ClearLaterAsync(version, 2500);
    }

    private async Task ClearLaterAsync(int version, int delayMs)
    {
        await Task.Delay(delayMs);
        if (version != _messageVersion) return;   // з'явилося нове повідомлення, це вже не актуально
        _message = null;
        UpdateStatus();
    }

    protected override void UpdateStatus()
    {
        if (_message != null)
        {
            StatusText = _message;
            StatusBrush = _messageIsError ? Brushes.Tomato : Brushes.LightGreen;
        }
        else
        {
            base.UpdateStatus();   // стан зв'язку обробляє базовий клас
        }
    }
    partial void OnInputTextChanged(string value)
    {
        // Користувач почав виправляти ввід: стара помилка вже не актуальна
        if (_messageIsError)
            SetMessage(null);
    }
}