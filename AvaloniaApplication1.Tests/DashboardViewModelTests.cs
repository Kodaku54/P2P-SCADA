using System;
using System.IO;
using System.Linq;
using AvaloniaApplication1.Models;
using AvaloniaApplication1.ViewModels;
using AvaloniaApplication1.ViewModels.Widgets;
using Xunit;

namespace AvaloniaApplication1.Tests;

public class DashboardViewModelTests
{
    private static readonly WidgetFactory Factory = new(tag => $"test_{tag.Tag}");

    private static DashboardConfig CreateSample() => new()
    {
        Name = "Котельня",
        Widgets =
        {
            new GaugeWidgetConfig { Title = "Температура", X = 450, Y = 20, Unit = "°C" },
            new ChartWidgetConfig { Title = "Графік", X = 20, Y = 20, MaxPoints = 50 }
        }
    };

    [Fact]
    public void CreatesWidgetsFromConfig()
    {
        var vm = new DashboardViewModel(CreateSample(), Factory);

        Assert.Equal("Котельня", vm.DashboardTitle);
        Assert.Equal(2, vm.Widgets.Count);
        Assert.IsType<GaugeWidgetViewModel>(vm.Widgets[0]);
        Assert.IsType<ChartWidgetViewModel>(vm.Widgets[1]);
    }

    [Fact]
    public void ToConfig_KeepsIdsAndReflectsChanges()
    {
        var original = CreateSample();
        var vm = new DashboardViewModel(original, Factory);

        // Користувач перейменував дашборд і пересунув перший віджет
        vm.DashboardTitle = "Нова котельня";
        vm.Widgets[0].X = 777;

        var saved = vm.ToConfig();

        Assert.Equal(original.Id, saved.Id);
        Assert.Equal("Нова котельня", saved.Name);
        Assert.Equal(2, saved.Widgets.Count);
        Assert.Equal(original.Widgets[0].Id, saved.Widgets[0].Id);
        Assert.Equal(777, saved.Widgets[0].X);
        Assert.IsType<GaugeWidgetConfig>(saved.Widgets[0]);
        Assert.IsType<ChartWidgetConfig>(saved.Widgets[1]);
    }

    [Fact]
    public void MissingBackgroundImage_DoesNotLoseFileNameInConfig()
    {
        var config = CreateSample();
        config.BackgroundFile = "no-such-file.png";
        string emptyFolder = Path.Combine(Path.GetTempPath(), "p2p-scada-no-assets-" + Guid.NewGuid());

        var vm = new DashboardViewModel(config, Factory, emptyFolder);

        Assert.Null(vm.BackgroundImage);                       // картинки немає
        Assert.Equal("no-such-file.png", vm.ToConfig().BackgroundFile);   // але назва не загубилась
    }
}