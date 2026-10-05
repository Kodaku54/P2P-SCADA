using System;
using System.Text.Json;
using AvaloniaApplication1.Models;
using AvaloniaApplication1.Services;
using Xunit;

namespace AvaloniaApplication1.Tests;

public class DashboardConfigTests
{
    private static DashboardConfig CreateSample(Guid deviceId) => new()
    {
        Name = "Котельня",
        BackgroundFile = "scheme.png",
        Widgets =
        {
            new GaugeWidgetConfig
            {
                Title = "Температура", X = 450, Y = 20, Unit = "°C",
                Min = 0, Max = 100, HighCritical = 90,
                Source = new TagRef { DeviceId = deviceId, Tag = "Temp" }
            },
            new ChartWidgetConfig
            {
                Title = "Графік", X = 20, Y = 20, YAxisLabel = "Unit", MaxPoints = 50,
                Source = new TagRef { DeviceId = deviceId, Tag = "Sensor1" }
            }
        }
    };

    [Fact]
    public void Dashboard_SurvivesJsonRoundTrip()
    {
        var deviceId = Guid.NewGuid();
        var original = CreateSample(deviceId);

        string json = JsonSerializer.Serialize(original, ConfigJson.Options);
        var loaded = JsonSerializer.Deserialize<DashboardConfig>(json, ConfigJson.Options)!;

        Assert.Equal(original.Id, loaded.Id);
        Assert.Equal("Котельня", loaded.Name);
        Assert.Equal("scheme.png", loaded.BackgroundFile);
        Assert.Equal(2, loaded.Widgets.Count);

        // Кожен віджет має повернутися своїм власним класом, а не базовим
        var gauge = Assert.IsType<GaugeWidgetConfig>(loaded.Widgets[0]);
        Assert.Equal("°C", gauge.Unit);
        Assert.Equal(90, gauge.HighCritical);
        Assert.Null(gauge.LowWarning);
        Assert.Equal(deviceId, gauge.Source.DeviceId);

        var chart = Assert.IsType<ChartWidgetConfig>(loaded.Widgets[1]);
        Assert.Equal(50, chart.MaxPoints);
        Assert.Equal("Sensor1", chart.Source.Tag);
    }

    [Fact]
    public void Json_HasTypeFieldAndReadableUkrainian()
    {
        string json = JsonSerializer.Serialize(CreateSample(Guid.NewGuid()), ConfigJson.Options);

        Assert.Contains("\"type\": \"gauge\"", json);
        Assert.Contains("\"type\": \"chart\"", json);
        Assert.Contains("Котельня", json);   // а не \u041A\u043E...
    }
}