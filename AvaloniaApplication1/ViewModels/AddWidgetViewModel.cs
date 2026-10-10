using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using AvaloniaApplication1.Models;
using AvaloniaApplication1.Services;
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

    public IReadOnlyList<DeviceOption> Devices { get; }
    public bool HasNoDevices => Devices.Count == 0;

    public string[] WidgetTypes { get; } = { "Gauge", "Chart" };
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
    private string _selectedType = "Gauge";

    public bool IsGauge => SelectedType == "Gauge";
    public bool IsChart => SelectedType == "Chart";

    [ObservableProperty] private string _title = "";
    [ObservableProperty] private string _unit = "";
    [ObservableProperty] private decimal? _min = 0;
    [ObservableProperty] private decimal? _max = 100;
    [ObservableProperty] private decimal? _maxPoints = 50;

    [ObservableProperty] private string? _errorMessage;

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

    partial void OnSelectedDeviceChanged(DeviceOption? value)
    {
        Sensors.Clear();
        if (value != null)
            foreach (var sensor in value.Sensors)
                Sensors.Add(sensor);

        SelectedSensor = Sensors.FirstOrDefault();

        // Якщо датчиків немає, одразу показуємо форму нового
        IsNewSensor = Sensors.Count == 0;
        ToggleNewSensorCommand.NotifyCanExecuteChanged();
    }

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
        if (SelectedDevice == null) return false;
        return IsNewSensor ? !string.IsNullOrWhiteSpace(NewTag) : SelectedSensor != null;
    }

    [RelayCommand(CanExecute = nameof(CanCreate))]
    private void Create()
    {
        ErrorMessage = null;
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
            Title = string.IsNullOrWhiteSpace(Title) ? sensorTag : Title.Trim(),
            Unit = Unit.Trim(),
            X = Math.Max(0, Math.Round(_x)),
            Y = Math.Max(0, Math.Round(_y))
        };

        if (IsGauge)
        {
            widget.Min = (double)(Min ?? 0);
            widget.Max = (double)(Max ?? 100);

            if (widget.Max <= widget.Min)
            {
                ErrorMessage = "Максимум має бути більшим за мінімум.";
                return;
            }
        }
        else
        {
            widget.MaxPoints = Math.Max(2, (int)(MaxPoints ?? 50));
        }

        Result = new AddWidgetResult(widget, newDevice, newSensor);
        CloseRequested?.Invoke();
    }

    [RelayCommand]
    private void Cancel() => CloseRequested?.Invoke();
}