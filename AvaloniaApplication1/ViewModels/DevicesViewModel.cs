using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using AvaloniaApplication1.Models;
using AvaloniaApplication1.Services;
using Avalonia.Controls;
using AvaloniaApplication1.Views;
using CommunityToolkit.Mvvm.Input;
using MsBox.Avalonia;
using MsBox.Avalonia.Enums;

namespace AvaloniaApplication1.ViewModels;

// Рядок датчика в списку
public sealed class SensorRow
{
    public required DeviceConfig Device { get; init; }
    public required SensorConfig Sensor { get; init; }

    public string Text =>
        $"{Sensor.Tag}   ·   {Sensor.Area} #{Sensor.Address}   ·   {Sensor.DataType}   ·   unit {Sensor.Unit}";
}

// Картка пристрою
public sealed class DeviceRow
{
    public required string Title { get; init; }
    public required string Subtitle { get; init; }
    public ObservableCollection<SensorRow> Sensors { get; } = new();
    public bool HasNoSensors => Sensors.Count == 0;
    public required DeviceConfig Device { get; init; }
    public decimal? UpdateRate { get; set; }   // мс; поле вводу, застосовується кнопкою
}

public sealed partial class DevicesViewModel : ViewModelBase
{
    private readonly AppConfig _config;
    private readonly Func<IEnumerable<DashboardViewModel>> _getDashboards;

    public ObservableCollection<DeviceRow> Devices { get; } = new();
    public bool IsEmpty => Devices.Count == 0;

    public DevicesViewModel(AppConfig config, Func<IEnumerable<DashboardViewModel>> getDashboards)
    {
        _config = config;
        _getDashboards = getDashboards;

        NetworkStatusService.Default.Changed += Refresh;
        Refresh();
    }

    // Перебудовує список із конфігу (викликається і при відкритті сторінки)
    public void Refresh()
    {
        var nodes = NetworkStatusService.Default.Nodes;

        Devices.Clear();
        foreach (var device in _config.Devices)
        {
            var node = nodes.FirstOrDefault(n => n.Id == device.Id);
            var mark = node == null ? "?" : node.Online ? "●" : "○";
            var name = string.IsNullOrWhiteSpace(device.Name) ? device.Id : device.Name;

            var row = new DeviceRow
            {
                Title = $"{mark} {name}",
                Subtitle = $"{device.IpAddress}   ·   ID {device.Id}",
                Device = device,
                UpdateRate = device.UpdateRateMs
            };

            foreach (var sensor in device.Sensors)
                row.Sensors.Add(new SensorRow { Device = device, Sensor = sensor });

            Devices.Add(row);
        }

        OnPropertyChanged(nameof(IsEmpty));
    }
    
    [RelayCommand]
    private void ApplyUpdateRate(DeviceRow? row)
    {
        if (row?.UpdateRate is not { } rate) return;

        row.Device.UpdateRateMs = (int)rate;   // decimal → int
        ConfigManager.Save(_config);
        DeviceManager.Default.Sync();          // надсилає Pi оновлену конфігурацію
    }

    [RelayCommand]
    private async Task DeleteSensorAsync(SensorRow? row)
    {
        if (row == null) return;

        var deviceId = row.Device.Id;
        var tag = row.Sensor.Tag;

        // Скільки віджетів на всіх дашбордах використовують цей датчик
        var used = _config.Dashboards
            .SelectMany(d => d.Widgets)
            .Count(w => w.DeviceId == deviceId && w.Sensor == tag);

        var text = used == 0
            ? $"Видалити датчик \"{tag}\"?"
            : $"Видалити датчик \"{tag}\"?\nВін використовується у віджетах: {used}. Їх також буде видалено.";

        var box = MessageBoxManager.GetMessageBoxStandard(
            "Видалення датчика", text, ButtonEnum.YesNo, MsBox.Avalonia.Enums.Icon.Warning);

        if (await box.ShowAsync() != ButtonResult.Yes) return;

        // 1. Віджети цього датчика зникають з усіх дашбордів (і відкріплених теж)
        foreach (var dashboard in _getDashboards())
            dashboard.RemoveWidgetsOf(deviceId, tag);

        // 2. Сам датчик зникає з пристрою
        row.Device.Sensors.Remove(row.Sensor);
        ConfigManager.Save(_config);
        DeviceManager.Default.Sync();   // Pi має дізнатися, що датчика більше немає
        Refresh();
    }
    public async Task EditSensorAsync(SensorRow row, Window owner)
    {
        // Чи є віджети вводу на цьому датчику (на всіх дашбордах)
        var usedByInput = _config.Dashboards
            .SelectMany(d => d.Widgets)
            .Any(w => w.DeviceId == row.Device.Id
                      && w.Sensor == row.Sensor.Tag
                      && w.Type.Equals("Input", StringComparison.OrdinalIgnoreCase));

        var dialogVm = new EditSensorViewModel(row.Sensor, usedByInput);
        var dialog = new EditSensorWindow { DataContext = dialogVm };
        dialogVm.CloseRequested += () => dialog.Close();

        await dialog.ShowDialog(owner);

        if (!dialogVm.Saved) return;   // скасовано

        ConfigManager.Save(_config);

        // Режим віджета вводу залежить від типу датчика, тому створюємо такі віджети заново
        foreach (var dashboard in _getDashboards())
            dashboard.RebuildInputWidgetsOf(row.Device.Id, row.Sensor.Tag);

        DeviceManager.Default.Sync();   // Pi отримує нові параметри датчика
        Refresh();
    }
}