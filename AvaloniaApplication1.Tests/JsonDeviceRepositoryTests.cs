using System;
using System.IO;
using System.Threading.Tasks;
using AvaloniaApplication1.Models;
using AvaloniaApplication1.Services;
using Xunit;

namespace AvaloniaApplication1.Tests;

public class JsonDeviceRepositoryTests : IDisposable
{
    private readonly string _root =
        Path.Combine(Path.GetTempPath(), "p2p-scada-device-tests-" + Guid.NewGuid());

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }

    [Fact]
    public async Task SaveThenLoad_KeepsIdAndSensors()
    {
        var repo = new JsonDeviceRepository(_root);
        var device = new DeviceConfig
        {
            Name = "Лабораторія", IpAddress = "100.85.42.12", Port = 10502,
            Sensors = { new SensorConfig { Tag = "Temp", Address = 2, DataType = "Float" } }
        };

        await repo.SaveAsync(device);
        var loaded = await repo.LoadAllAsync();

        var single = Assert.Single(loaded);
        Assert.Equal(device.Id, single.Id);
        Assert.Equal("100.85.42.12", single.IpAddress);
        Assert.Equal(10502, single.Port);
        var sensor = Assert.Single(single.Sensors);
        Assert.Equal("Float", sensor.DataType);
    }

    [Fact]
    public async Task BrokenFile_IsSkipped()
    {
        var repo = new JsonDeviceRepository(_root);
        await repo.SaveAsync(new DeviceConfig { Name = "Справний" });
        File.WriteAllText(Path.Combine(_root, "devices", "broken.json"), "{ це не json");

        var loaded = await repo.LoadAllAsync();

        Assert.Equal("Справний", Assert.Single(loaded).Name);
    }
}