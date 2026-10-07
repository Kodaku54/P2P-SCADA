using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using AvaloniaApplication1.Models;
using AvaloniaApplication1.ViewModels;
using AvaloniaApplication1.ViewModels.Widgets;

namespace AvaloniaApplication1.Services;

public static class DashboardFactory
{
    public static string TagId(string deviceId, string sensor) => $"{deviceId}/{sensor}";

    public static List<DashboardViewModel> Create(AppConfig config) =>
        config.Dashboards
            .Select(d => CreateDashboard(config, d))
            .OfType<DashboardViewModel>()   // відкидає null
            .ToList();

    public static DashboardViewModel? CreateDashboard(AppConfig config, DashboardConfig dash)
    {
        var device = config.Devices.FirstOrDefault(d => d.Id == dash.DeviceId);
        if (device == null)
        {
            Debug.WriteLine($"[Config] Дашборд '{dash.Title}': невідомий пристрій '{dash.DeviceId}', пропущено");
            return null;
        }

        var widgets = new List<WidgetViewModelBase>();
        foreach (var w in dash.Widgets)
        {
            var widget = CreateWidget(device, w);
            if (widget != null) widgets.Add(widget);
        }

        return new DashboardViewModel(device.Id, dash.Title, dash.BackgroundImage, widgets);
    }

    private static WidgetViewModelBase? CreateWidget(DeviceConfig device, WidgetConfig w)
    {
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
                return new GaugeWidgetViewModel(tagId, title, w.Unit, w.Min, w.Max, w.LowWarning, w.HighCritical)
                {
                    X = w.X, Y = w.Y, Width = w.Width, Height = w.Height
                };

            case "chart":
                return new ChartWidgetViewModel(tagId, title, w.Unit, w.MaxPoints)
                {
                    X = w.X, Y = w.Y, Width = w.Width, Height = w.Height
                };

            default:
                Debug.WriteLine($"[Config] Невідомий тип віджета '{w.Type}', пропущено");
                return null;
        }
    }

    // Симулятор для всіх віджетів із конфігу (діапазон бере з Min/Max віджета)
    public static DataSimulator? CreateSimulator(AppConfig config)
    {
        if (!config.UseSimulator) return null;

        var simulator = new DataSimulator(500);
        var seen = new HashSet<string>();
        var index = 0;

        foreach (var dash in config.Dashboards)
        foreach (var w in dash.Widgets)
        {
            var tagId = TagId(dash.DeviceId, w.Sensor);
            if (!seen.Add(tagId)) continue;

            simulator.Add(tagId, w.Min, w.Max, periodSeconds: 15 + 5 * index++);
        }

        return simulator;
    }
}