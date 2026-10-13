using System.Collections.Generic;

namespace AvaloniaApplication1.Models;

public class SensorConfig
{
    public string Tag { get; set; } = string.Empty;   // id датчика в межах пристрою, напр. "boiler_temp"
    public int Unit { get; set; } = 1;                // Modbus unit id (slave)
    public string Area { get; set; } = "holding";     // coil | discrete | holding | input
    public int Address { get; set; }
    public string DataType { get; set; } = "Short";
}

public class DeviceConfig
{
    public string Id { get; set; } = string.Empty;          // Tailscale node ID (поле "ID" зі status --json)
    public string Name { get; set; } = string.Empty;
    public string IpAddress { get; set; } = string.Empty;   // запасний варіант, якщо IP не вдалось дізнатись із Tailscale
    public int Port { get; set; } = 502;
    public int UpdateRateMs { get; set; } = 500;
    public List<SensorConfig> Sensors { get; set; } = new();
}