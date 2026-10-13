using System.Collections.Generic;
using System.Text.Json.Serialization;

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
    public string DeviceId { get; set; } = string.Empty;   // DeviceConfig.Id
    public string Sensor { get; set; } = string.Empty;     // SensorConfig.Tag
    public string Title { get; set; } = string.Empty;
    public string Unit { get; set; } = string.Empty;

    public double X { get; set; }
    public double Y { get; set; }
    public double Width { get; set; } = 300;
    public double Height { get; set; } = 200;

    // Поля одного типу віджета не записуємо в JSON, коли вони порожні
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public double? Min { get; set; }          // Gauge
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public double? Max { get; set; }          // Gauge
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public double? LowWarning { get; set; }   // Gauge
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public double? HighCritical { get; set; } // Gauge
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public int? MaxPoints { get; set; }       // Chart
}