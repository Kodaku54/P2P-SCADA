using System.Collections.Generic;
using AvaloniaApplication1.Models;

namespace AvaloniaApplication1.Services;

/// <summary>
/// Тестові дані для першого запуску, коли на диску ще нічого немає.
/// Коли з'явиться справжній інтерфейс додавання пристроїв, цей клас можна видалити.
/// </summary>
public static class SampleData
{
    public static List<DeviceConfig> CreateDevices() => new()
    {
        new DeviceConfig { Name = "Лабораторія RPi 1", IpAddress = "100.85.42.12" },
        new DeviceConfig { Name = "Лабораторія 2 RPi 3", IpAddress = "100.85.42.13" }
    };

    public static DashboardConfig CreateDashboard(DeviceConfig device) => new()
    {
        Name = device.Name,
        BackgroundFile = "avares://AvaloniaApplication1/Assets/scheme.jpeg",
        Widgets =
        {
            new ChartWidgetConfig
            {
                Title = "Датчик з Малинки", YAxisLabel = "Unit", MaxPoints = 50,
                X = 20, Y = 20, Width = 300, Height = 200,
                Source = new TagRef { DeviceId = device.Id, Tag = "Sensor1" }
            },
            new GaugeWidgetConfig
            {
                Title = "Температура", Unit = "°C", Min = 0, Max = 100,
                X = 450, Y = 20, Width = 300, Height = 200,
                Source = new TagRef { DeviceId = device.Id, Tag = "Temp" }
            }
        }
    };
}