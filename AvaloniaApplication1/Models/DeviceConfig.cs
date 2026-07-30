using System.Collections.Generic;

namespace AvaloniaApplication1.Models;

public class SensorConfig
{
    public string Tag { get; set; } = string.Empty;
    public int Address { get; set; }
    public string DataType { get; set; } = "Short";
}

public class DeviceConfig
{
    public string IpAddress { get; set; } = string.Empty;
    public int Port { get; set; } = 502;
    public int UpdateRateMs { get; set; } = 500;
    public List<SensorConfig> Sensors { get; set; } = new();
}