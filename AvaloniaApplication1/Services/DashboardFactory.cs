using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using AvaloniaApplication1.Models;
using AvaloniaApplication1.ViewModels;
using AvaloniaApplication1.ViewModels.Widgets;

namespace AvaloniaApplication1.Services;

public static class DashboardFactory
{
    // Єдине місце, де складається TagId: "{deviceId}/{sensorId}"
    public static string TagId(string deviceId, string sensor) => $"{deviceId}/{sensor}";

    public static List<DashboardViewModel> Create(AppConfig config, DataSimulator? simulator) =>
        config.Dashboards.Select(d => CreateDashboard(config, d, simulator)).ToList();

    public static DashboardViewModel CreateDashboard(AppConfig config, DashboardConfig dash, DataSimulator? simulator)
    {
        var widgets = new List<WidgetViewModelBase>();
        foreach (var w in dash.Widgets)
        {
            var widget = CreateWidget(config, w);
            if (widget != null) widgets.Add(widget);
        }

        return new DashboardViewModel(config, dash, widgets, simulator);
    }

    public static WidgetViewModelBase? CreateWidget(AppConfig config, WidgetConfig w)
    {
        // Кожен віджет сам вказує, з якого пристрою його датчик
        var device = config.Devices.FirstOrDefault(d => d.Id == w.DeviceId);
        if (device == null)
        {
            Debug.WriteLine($"[Config] Віджет '{w.Title}': невідомий пристрій '{w.DeviceId}', пропущено");
            return null;
        }

        if (device.Sensors.All(s => s.Tag != w.Sensor))
        {
            Debug.WriteLine($"[Config] Віджет '{w.Title}': у пристрої '{device.Id}' немає датчика '{w.Sensor}', пропущено");
            return null;
        }

        var tagId = TagId(device.Id, w.Sensor);
        var title = string.IsNullOrWhiteSpace(w.Title) ? w.Sensor : w.Title;

        switch (w.Type.ToLowerInvariant())
        {
            case "gauge":
                return new GaugeWidgetViewModel(tagId, title, w.Unit, w.Min ?? 0, w.Max ?? 100, w.LowWarning, w.HighCritical)
                {
                    X = w.X, Y = w.Y, Width = w.Width, Height = w.Height, Config = w
                };

            case "chart":
                return new ChartWidgetViewModel(tagId, title, w.Unit, w.MaxPoints ?? 50)
                {
                    X = w.X, Y = w.Y, Width = w.Width, Height = w.Height, Config = w
                };
            default:
                Debug.WriteLine($"[Config] Невідомий тип віджета '{w.Type}', пропущено");
                return null;
        }
    }

    // Симулятор для всіх віджетів із конфігу
    public static DataSimulator? CreateSimulator(AppConfig config)
    {
        if (!config.UseSimulator) return null;

        var simulator = new DataSimulator(500);
        foreach (var w in config.Dashboards.SelectMany(d => d.Widgets))
            AddSimulatedSignal(simulator, w);

        return simulator;
    }

    // Підключає тег віджета до симулятора (діапазон з Min/Max, для графіка 0..100)
    public static void AddSimulatedSignal(DataSimulator simulator, WidgetConfig w)
    {
        if (string.IsNullOrWhiteSpace(w.DeviceId)) return;

        var tagId = TagId(w.DeviceId, w.Sensor);

        // Стабільний "випадковий" період, щоб різні сигнали не рухались синхронно
        var hash = 0;
        foreach (var c in tagId) hash += c;

        simulator.Add(tagId, w.Min ?? 0, w.Max ?? 100, periodSeconds: 15 + hash % 15);
    }
}