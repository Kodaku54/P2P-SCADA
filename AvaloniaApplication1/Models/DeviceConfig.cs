using System;
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
    // Незмінний ідентифікатор: віджети посилаються на пристрій саме за ним, а не за IP
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;

    public string IpAddress { get; set; } = string.Empty;
    public int Port { get; set; } = 502;
    public int UpdateRateMs { get; set; } = 500;
    public List<SensorConfig> Sensors { get; set; } = new();
}