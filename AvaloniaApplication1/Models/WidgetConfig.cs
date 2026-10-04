using System;
using System.Text.Json.Serialization;

namespace AvaloniaApplication1.Models;

public class TagRef
{
    public Guid DeviceId { get; set; }
    public string Tag { get; set; } = string.Empty;
}

[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(GaugeWidgetConfig), "gauge")]
[JsonDerivedType(typeof(ChartWidgetConfig), "chart")]
public abstract class WidgetConfig
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Title { get; set; } = string.Empty;

    // Позиція і розмір на полотні (Canvas)
    public double X { get; set; }
    public double Y { get; set; }
    public double Width { get; set; } = 300;
    public double Height { get; set; } = 200;

    // З якого датчика віджет бере дані
    public TagRef Source { get; set; } = new();
}
public class GaugeWidgetConfig : WidgetConfig
{
    public string Unit { get; set; } = string.Empty;
    public double Min { get; set; } = 0;
    public double Max { get; set; } = 100;
    
    public double? LowWarning { get; set; }
    public double? HighCritical { get; set; }
}

public class ChartWidgetConfig : WidgetConfig
{
    public string YAxisLabel { get; set; } = string.Empty;

    // Скільки останніх точок показувати на графіку
    public int MaxPoints { get; set; } = 50;
}
