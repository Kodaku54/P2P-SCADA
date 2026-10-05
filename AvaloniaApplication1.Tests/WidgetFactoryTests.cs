using System;
using AvaloniaApplication1.Models;
using AvaloniaApplication1.ViewModels.Widgets;
using Xunit;

namespace AvaloniaApplication1.Tests;

public class WidgetFactoryTests
{
    private static readonly WidgetFactory Factory = new(tag => $"test_{tag.Tag}");

    private class UnknownWidgetConfig : WidgetConfig { }

    [Fact]
    public void Gauge_ConfigToViewModelAndBack_KeepsEverything()
    {
        var deviceId = Guid.NewGuid();
        var config = new GaugeWidgetConfig
        {
            Title = "Температура", X = 450, Y = 20, Width = 300, Height = 200,
            Unit = "°C", Min = 10, Max = 90, HighCritical = 85,   // LowWarning лишаємо null
            Source = new TagRef { DeviceId = deviceId, Tag = "Temp" }
        };

        var vm = Assert.IsType<GaugeWidgetViewModel>(Factory.Create(config));
        Assert.Equal("test_Temp", vm.DataSourceTag);
        Assert.Equal("Температура", vm.Title);   // саме з бази, без затінення
        Assert.Equal(450, vm.X);

        var back = Assert.IsType<GaugeWidgetConfig>(vm.ToConfig());
        Assert.Equal(config.Id, back.Id);
        Assert.Equal("Температура", back.Title);
        Assert.Equal(450, back.X);
        Assert.Equal(20, back.Y);
        Assert.Equal(300, back.Width);
        Assert.Equal(200, back.Height);
        Assert.Equal("°C", back.Unit);
        Assert.Equal(10, back.Min);
        Assert.Equal(90, back.Max);
        Assert.Null(back.LowWarning);            // порожнє значення не стало числом
        Assert.Equal(85, back.HighCritical);
        Assert.Equal(deviceId, back.Source.DeviceId);
        Assert.Equal("Temp", back.Source.Tag);
    }

    [Fact]
    public void MovingWidget_IsReflectedInSavedConfig()
    {
        var vm = Factory.Create(new GaugeWidgetConfig { X = 10, Y = 10 });

        vm.X = 300;
        vm.Y = 120;

        var saved = vm.ToConfig();
        Assert.Equal(300, saved.X);
        Assert.Equal(120, saved.Y);
    }

    [Fact]
    public void UnknownConfigType_Throws()
    {
        Assert.Throws<NotSupportedException>(() => Factory.Create(new UnknownWidgetConfig()));
    }

    [Fact]
    public void Chart_ConfigToViewModelAndBack_KeepsEverything()
    {
        var deviceId = Guid.NewGuid();
        var config = new ChartWidgetConfig
        {
            Title = "Графік", X = 20, Y = 20, YAxisLabel = "Unit", MaxPoints = 80,
            Source = new TagRef { DeviceId = deviceId, Tag = "Sensor1" }
        };

        var vm = Assert.IsType<ChartWidgetViewModel>(Factory.Create(config));
        var back = Assert.IsType<ChartWidgetConfig>(vm.ToConfig());

        Assert.Equal(config.Id, back.Id);
        Assert.Equal("Графік", back.Title);
        Assert.Equal("Unit", back.YAxisLabel);
        Assert.Equal(80, back.MaxPoints);
        Assert.Equal(deviceId, back.Source.DeviceId);
        Assert.Equal("Sensor1", back.Source.Tag);
    }
}