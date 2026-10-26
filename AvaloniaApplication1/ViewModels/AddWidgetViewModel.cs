using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using AvaloniaApplication1.Models;
using AvaloniaApplication1.Services;
using AvaloniaApplication1.ViewModels.Widgets;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AvaloniaApplication1.ViewModels;

// Що повертає вікно: віджет + (за потреби) новий пристрій і новий датчик
public record AddWidgetResult(WidgetConfig Widget, DeviceConfig? NewDevice, SensorConfig? NewSensor);

// Пристрій у списку: або вже налаштований, або виявлений у Tailscale, але ще не доданий
public sealed class DeviceOption
{
    public DeviceConfig? Existing { get; init; }
    public TailscaleNode? Node { get; init; }
    public string Display { get; init; } = "";

    public string Id => Existing?.Id ?? Node!.Id;

    public IReadOnlyList<SensorConfig> Sensors =>
        (IReadOnlyList<SensorConfig>?)Existing?.Sensors ?? Array.Empty<SensorConfig>();
}

public sealed partial class AddWidgetViewModel : ViewModelBase
{
    private readonly double _x;
    private readonly double _y;
    private readonly WidgetConfig? _existing;

    public bool IsEditing => _existing != null;
    public string WindowTitle => IsEditing ? "Налаштування віджета" : "Новий віджет";
    public string ActionText => IsEditing ? "Зберегти" : "Створити";
    public string SensorInfo => _existing == null ? "" : $"{_existing.DeviceId} / {_existing.Sensor}";

    [ObservableProperty] private decimal? _widgetWidth = 300;
    [ObservableProperty] private decimal? _widgetHeight = 200;
    [ObservableProperty] private decimal? _widgetX = 0;
    [ObservableProperty] private decimal? _widgetY = 0;

    public IReadOnlyList<DeviceOption> Devices { get; }
    public bool HasNoDevices => Devices.Count == 0;

    public string[] WidgetTypes { get; } = { "Gauge", "Chart", "Indicator", "Note", "Input" };
    public string[] Areas { get; } = { "holding", "input", "coil", "discrete" };
    public string[] DataTypes { get; } = { "Short", "Float", "Bool" };

    // Датчики обраного пристрою
    public ObservableCollection<SensorConfig> Sensors { get; } = new();

    // ---- пристрій і датчик ----
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CreateCommand))]
    private DeviceOption? _selectedDevice;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CreateCommand))]
    private SensorConfig? _selectedSensor;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CreateCommand))]
    [NotifyPropertyChangedFor(nameof(NewSensorToggleText))]
    private bool _isNewSensor;

    public string NewSensorToggleText => IsNewSensor ? "Обрати наявний датчик" : "＋ Новий датчик";

    // ---- поля нового датчика ----
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CreateCommand))]
    private string _newTag = "";

    [ObservableProperty] private decimal? _newUnitId = 1;
    [ObservableProperty] private decimal? _newAddress = 0;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsDataTypeEditable))]
    private string _newArea = "holding";

    [ObservableProperty] private string _newDataType = "Short";

    // Для coil і discrete input тип даних завжди Bool
    public bool IsDataTypeEditable => NewArea is "holding" or "input";

    // ---- віджет ----
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsGauge))]
    [NotifyPropertyChangedFor(nameof(IsChart))]
    [NotifyPropertyChangedFor(nameof(IsIndicator))]
    [NotifyPropertyChangedFor(nameof(IsNote))]
    [NotifyPropertyChangedFor(nameof(HasUnit))]
    [NotifyPropertyChangedFor(nameof(ShowSensorPicker))]
    [NotifyPropertyChangedFor(nameof(ShowSensorInfo))]
    private string _selectedType = "Gauge";

    public bool IsGauge => SelectedType == "Gauge";
    public bool IsChart => SelectedType == "Chart";
    public bool IsIndicator => SelectedType == "Indicator";
    public bool IsNote => SelectedType == "Note";
    public bool HasUnit => IsGauge || IsChart;               // одиниці мають сенс лише для числових віджетів
    public bool ShowSensorPicker => !IsEditing && !IsNote;   // нотатка не прив'язана до датчика
    public bool ShowSensorInfo => IsEditing && !IsNote;
    public bool IsInput => SelectedType == "Input";
    public bool InputIsBool => CurrentDataType == "Bool";
    public bool InputIsButton => IsInput && InputIsBool;
    public bool InputIsNumeric => IsInput && !InputIsBool;
    
    // Тип обраного датчика (під час редагування береться з конфігу)
    private string? CurrentDataType =>
        IsEditing ? _editSensorType : IsNewSensor ? NewDataType : SelectedSensor?.DataType;

    private readonly string? _editSensorType;
    
    [ObservableProperty] private string _onText = IndicatorWidgetViewModel.DefaultOnText;
    [ObservableProperty] private string _offText = IndicatorWidgetViewModel.DefaultOffText;
    [ObservableProperty] private string _noteText = "";
    [ObservableProperty] private decimal? _fontSize = 16;


    [ObservableProperty] private string _title = "";
    [ObservableProperty] private string _unit = "";
    [ObservableProperty] private decimal? _min = 0;
    [ObservableProperty] private decimal? _max = 100;
    [ObservableProperty] private decimal? _maxPoints = 50;

    [ObservableProperty] private string? _errorMessage;
    
    [ObservableProperty] private decimal? _writeValue = 1;   // Bool: що записує кнопка
    [ObservableProperty] private decimal? _inputMin;         // числа: межі введення (порожньо = без обмежень)
    [ObservableProperty] private decimal? _inputMax;

    // Заповнюється після "Створити"; null, якщо діалог скасовано
    public AddWidgetResult? Result { get; private set; }

    public event Action? CloseRequested;

    public AddWidgetViewModel(AppConfig config, double x, double y)
    {
        _x = x;
        _y = y;
        Devices = BuildDeviceOptions(config);
        SelectedDevice = Devices.Count > 0 ? Devices[0] : null;
    }
    public AddWidgetViewModel(AppConfig config, WidgetConfig existing)
    {
        _existing = existing;
        Devices = Array.Empty<DeviceOption>();

        SelectedType = WidgetTypes.FirstOrDefault(t => t.Equals(existing.Type, StringComparison.OrdinalIgnoreCase)) ?? "Gauge";
        OnText = existing.OnText ?? IndicatorWidgetViewModel.DefaultOnText;
        OffText = existing.OffText ?? IndicatorWidgetViewModel.DefaultOffText;
        NoteText = existing.Text ?? "";
        FontSize = (decimal)(existing.FontSize ?? 16);
        Title = existing.Title;
        Unit = existing.Unit;
        Min = (decimal)(existing.Min ?? 0);
        Max = (decimal)(existing.Max ?? 100);
        MaxPoints = existing.MaxPoints ?? 50;
        WidgetWidth = (decimal)existing.Width;
        WidgetHeight = (decimal)existing.Height;
        WidgetX = (decimal)existing.X;
        WidgetY = (decimal)existing.Y;
        
        _editSensorType = config.Devices.FirstOrDefault(d => d.Id == existing.DeviceId)
            ?.Sensors.FirstOrDefault(s => s.Tag == existing.Sensor)?.DataType;
        InputMin = existing.Min.HasValue ? (decimal)existing.Min.Value : null;
        InputMax = existing.Max.HasValue ? (decimal)existing.Max.Value : null;
        WriteValue = (decimal)(existing.WriteValue ?? 1);
    }
    // Переносить поля віджета з діалогу в конфіг; false, якщо є помилка валідації
    private bool ApplyWidgetSettings(WidgetConfig widget)
    {
        switch (SelectedType)
        {
            case "Gauge":
                var min = (double)(Min ?? 0);
                var max = (double)(Max ?? 100);
                if (max <= min)
                {
                    ErrorMessage = "Максимум має бути більшим за мінімум.";
                    return false;
                }
                widget.Min = min;
                widget.Max = max;
                break;

            case "Chart":
                widget.MaxPoints = Math.Max(2, (int)(MaxPoints ?? 50));
                break;

            case "Indicator":
                widget.OnText = string.IsNullOrWhiteSpace(OnText) ? IndicatorWidgetViewModel.DefaultOnText : OnText.Trim();
                widget.OffText = string.IsNullOrWhiteSpace(OffText) ? IndicatorWidgetViewModel.DefaultOffText : OffText.Trim();
                break;

            case "Note":
                if (string.IsNullOrWhiteSpace(NoteText))
                {
                    ErrorMessage = "Введіть текст нотатки.";
                    return false;
                }
                widget.Text = NoteText.Trim();
                widget.FontSize = Math.Clamp((double)(FontSize ?? 16), 8, 200);
                widget.Title = "Нотатка";   // використовується в запиті на видалення
                widget.Unit = "";
                return true;
            
            case "Input":
                if (InputIsBool)
                {
                    widget.WriteValue = (double)(WriteValue ?? 1);
                }
                else
                {
                    if (InputMin is { } lowLimit && InputMax is { } highLimit && highLimit <= lowLimit)
                    {
                        ErrorMessage = "Максимум має бути більшим за мінімум.";
                        return false;
                    }
                    widget.Min = InputMin.HasValue ? (double)InputMin.Value : null;
                    widget.Max = InputMax.HasValue ? (double)InputMax.Value : null;
                }
                break;
        }

        widget.Title = string.IsNullOrWhiteSpace(Title) ? widget.Sensor : Title.Trim();
        widget.Unit = (Unit ?? "").Trim();
        
        return true;
    }

    private static List<DeviceOption> BuildDeviceOptions(AppConfig config)
    {
        var nodes = NetworkStatusService.Default.Nodes;
        var options = new List<DeviceOption>();

        // Налаштовані пристрої (з живим статусом, якщо вузол знайдено в Tailscale)
        foreach (var device in config.Devices)
        {
            var node = nodes.FirstOrDefault(n => n.Id == device.Id);
            var name = string.IsNullOrWhiteSpace(device.Name) ? device.Id : device.Name;
            options.Add(new DeviceOption { Existing = device, Node = node, Display = $"{StatusMark(node)} {name}" });
        }

        // Виявлені в Tailscale, яких у конфігу ще немає
        foreach (var node in nodes.Where(n => config.Devices.All(d => d.Id != n.Id)))
            options.Add(new DeviceOption { Node = node, Display = $"{StatusMark(node)} {node.HostName} (новий)" });

        return options;
    }

    // ● на зв'язку, ○ не в мережі, ? не знайдено в Tailscale
    private static string StatusMark(TailscaleNode? node) => node == null ? "?" : node.Online ? "●" : "○";

    private void RefreshSensors()
    {
        Sensors.Clear();
        if (SelectedDevice != null)
            foreach (var sensor in SelectedDevice.Sensors)
                if (!IsInput || sensor.Area is "coil" or "holding")
                    Sensors.Add(sensor);

        SelectedSensor = Sensors.FirstOrDefault();

        // Якщо датчиків немає, одразу показуємо форму нового
        IsNewSensor = Sensors.Count == 0;
        ToggleNewSensorCommand.NotifyCanExecuteChanged();
    }

    partial void OnSelectedDeviceChanged(DeviceOption? value) => RefreshSensors();
    partial void OnSelectedTypeChanged(string value) => RefreshSensors();

    partial void OnNewAreaChanged(string value)
    {
        if (value is "coil" or "discrete")
            NewDataType = "Bool";
        else if (NewDataType == "Bool")
            NewDataType = "Short";
    }

    private bool CanToggleNewSensor() => Sensors.Count > 0;

    [RelayCommand(CanExecute = nameof(CanToggleNewSensor))]
    private void ToggleNewSensor() => IsNewSensor = !IsNewSensor;

    private bool CanCreate()
    {
        if (IsEditing) return true;
        if (IsNote) return true;
        if (SelectedDevice == null) return false;
        return IsNewSensor ? !string.IsNullOrWhiteSpace(NewTag) : SelectedSensor != null;
    }
    // Розмір нового віджета за замовчуванням
    private static (double Width, double Height) DefaultSize(string type, bool isBool = false) => type switch
    {
        "Indicator" => (200.0, 120.0),
        "Note" => (300.0, 60.0),
        "Input" => isBool ? (200.0, 100.0) : (260.0, 130.0),
        _ => (300.0, 200.0)
    };

    [RelayCommand(CanExecute = nameof(CanCreate))]
    private void Create()
    {
        ErrorMessage = null;

        if (_existing != null)
        {
            if (!ApplyWidgetSettings(_existing)) return;

            _existing.X = Math.Max(0, (double)(WidgetX ?? 0));
            _existing.Y = Math.Max(0, (double)(WidgetY ?? 0));
            _existing.Width = DefaultSize(SelectedType).Width;
            _existing.Height = DefaultSize(SelectedType).Height;

            Result = new AddWidgetResult(_existing, null, null);
            CloseRequested?.Invoke();
            return;
        }
        if (IsNote)
        {
            var size = DefaultSize("Note");
            var note = new WidgetConfig
            {
                Type = "Note",
                X = Math.Max(0, Math.Round(_x)),
                Y = Math.Max(0, Math.Round(_y)),
                Width = size.Width,
                Height = size.Height
            };

            if (!ApplyWidgetSettings(note)) return;

            Result = new AddWidgetResult(note, null, null);
            CloseRequested?.Invoke();
            return;
        }

        var option = SelectedDevice!;

        // --- датчик ---
        SensorConfig? newSensor = null;
        string sensorTag;

        if (IsNewSensor)
        {
            sensorTag = NewTag.Trim();

            if (sensorTag.Contains('/'))
            {
                ErrorMessage = "Ім'я датчика не може містити символ \"/\".";
                return;
            }
            
            if (IsInput && NewArea is not ("coil" or "holding"))
            {
                ErrorMessage = "Для віджета вводу потрібна функція coil або holding.";
                return;
            }

            if (option.Sensors.Any(s => string.Equals(s.Tag, sensorTag, StringComparison.OrdinalIgnoreCase)))
            {
                ErrorMessage = $"У цього пристрою вже є датчик \"{sensorTag}\".";
                return;
            }

            newSensor = new SensorConfig
            {
                Tag = sensorTag,
                Unit = (int)(NewUnitId ?? 1),
                Area = NewArea,
                Address = (int)(NewAddress ?? 0),
                DataType = NewDataType
            };
        }
        else
        {
            sensorTag = SelectedSensor!.Tag;
        }

        // --- пристрій: якщо обрано вузол Tailscale, якого ще немає в конфігу ---
        DeviceConfig? newDevice = null;
        if (option.Existing == null)
        {
            var node = option.Node!;
            newDevice = new DeviceConfig { Id = node.Id, Name = node.HostName, IpAddress = node.Ip ?? "" };
        }

        // --- віджет ---
        var widget = new WidgetConfig
        {
            Type = SelectedType,
            DeviceId = option.Id,
            Sensor = sensorTag,
            X = Math.Max(0, Math.Round(_x)),
            Y = Math.Max(0, Math.Round(_y))
        };

        if (!ApplyWidgetSettings(widget)) return;

        Result = new AddWidgetResult(widget, newDevice, newSensor);
        CloseRequested?.Invoke();
    }
    protected override void OnPropertyChanged(PropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);

        if (e.PropertyName is nameof(SelectedType) or nameof(SelectedSensor) or nameof(IsNewSensor) or nameof(NewDataType))
        {
            OnPropertyChanged(nameof(IsInput));
            OnPropertyChanged(nameof(InputIsBool));
            OnPropertyChanged(nameof(InputIsButton));
            OnPropertyChanged(nameof(InputIsNumeric));
            OnPropertyChanged(nameof(HasUnit));
        }
    }

    [RelayCommand]
    private void Cancel() => CloseRequested?.Invoke();
}