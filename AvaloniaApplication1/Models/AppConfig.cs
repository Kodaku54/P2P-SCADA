using System.Collections.Generic;

namespace AvaloniaApplication1.Models;

public class AppConfig
{
    public int Version { get; set; } = 1;

    // true: значення генерує DataSimulator (розробка без Pi)
    public bool UseSimulator { get; set; }

    public List<DeviceConfig> Devices { get; set; } = new();
    public List<DashboardConfig> Dashboards { get; set; } = new();
}

public class DashboardConfig
{
    public string Title { get; set; } = "Дашборд";
    public string? BackgroundImage { get; set; }
    public List<WidgetConfig> Widgets { get; set; } = new();
}

public class WidgetConfig
{
    public string Type { get; set; } = "Gauge";            // "Gauge" або "Chart"
    public string Sensor { get; set; } = string.Empty;     // SensorConfig.Tag
    public string DeviceId { get; set; } = string.Empty;   // DeviceConfig.Id
    public string Title { get; set; } = string.Empty;
    public string Unit { get; set; } = string.Empty;

    public double X { get; set; }
    public double Y { get; set; }
    public double Width { get; set; } = 300;
    public double Height { get; set; } = 200;

    public double Min { get; set; }
    public double Max { get; set; } = 100;
    public double? LowWarning { get; set; }               // тільки Gauge
    public double? HighCritical { get; set; }             // тільки Gauge
    public int MaxPoints { get; set; } = 50;              // тільки Chart
}