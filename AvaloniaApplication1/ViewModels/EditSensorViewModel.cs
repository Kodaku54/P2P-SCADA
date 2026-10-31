using System;
using AvaloniaApplication1.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AvaloniaApplication1.ViewModels;

// Діалог редагування датчика. Значення копіюються в поля діалогу
// і потрапляють у SensorConfig лише після «Зберегти», тож «Скасувати» нічого не змінює.
public sealed partial class EditSensorViewModel : ViewModelBase
{
    private readonly SensorConfig _sensor;
    private readonly bool _usedByInputWidgets;

    public string Tag => _sensor.Tag;
    public string[] Areas { get; } = { "holding", "input", "coil", "discrete" };
    public string[] DataTypes { get; } = { "Short", "Float", "Bool" };

    [ObservableProperty] private decimal? _unitId;
    [ObservableProperty] private decimal? _address;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsDataTypeEditable))]
    private string _area = "holding";

    [ObservableProperty] private string _dataType = "Short";
    [ObservableProperty] private string? _errorMessage;

    // Для coil і discrete тип завжди Bool, тож вибір доступний лише для holding і input
    public bool IsDataTypeEditable => Area is "holding" or "input";

    // true, якщо користувач натиснув «Зберегти»
    public bool Saved { get; private set; }

    public event Action? CloseRequested;

    public EditSensorViewModel(SensorConfig sensor, bool usedByInputWidgets)
    {
        _sensor = sensor;
        _usedByInputWidgets = usedByInputWidgets;

        UnitId = sensor.Unit;
        Address = sensor.Address;
        Area = sensor.Area;           // спершу функція, бо її зміна може підставити тип даних
        DataType = sensor.DataType;   // а потім справжній тип датчика
    }

    // Цей метод згенерує CommunityToolkit для властивості Area, він викликається після кожної її зміни
    partial void OnAreaChanged(string value)
    {
        if (value is "coil" or "discrete")
            DataType = "Bool";
        else if (DataType == "Bool")
            DataType = "Short";
    }

    [RelayCommand]
    private void Save()
    {
        ErrorMessage = null;

        // "x is "a" or "b"": чи дорівнює x одному з двох значень
        if (Area is "holding" or "input" && DataType == "Bool")
        {
            ErrorMessage = "Для holding і input тип Bool не підходить.";
            return;
        }

        if (_usedByInputWidgets && Area is not ("coil" or "holding"))
        {
            ErrorMessage = "Датчик використовують віджети вводу, тому функція має бути coil або holding.";
            return;
        }

        _sensor.Unit = (int)(UnitId ?? 1);          // decimal? → int
        _sensor.Address = (int)(Address ?? 0);
        _sensor.Area = Area;
        _sensor.DataType = DataType;

        Saved = true;
        CloseRequested?.Invoke();
    }

    [RelayCommand]
    private void Cancel() => CloseRequested?.Invoke();
}