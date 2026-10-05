using System;
using System.IO;
using System.Threading.Tasks;
using AvaloniaApplication1.Models;
using AvaloniaApplication1.Services;
using Xunit;

namespace AvaloniaApplication1.Tests;

public class JsonDashboardRepositoryTests : IDisposable
{
    // Кожен тест працює у власній тимчасовій папці й прибирає за собою
    private readonly string _root =
        Path.Combine(Path.GetTempPath(), "p2p-scada-tests-" + Guid.NewGuid());

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }

    [Fact]
    public async Task SaveThenLoad_ReturnsSameDashboard()
    {
        var repo = new JsonDashboardRepository(_root);
        var dashboard = new DashboardConfig { Name = "Котельня" };
        dashboard.Widgets.Add(new GaugeWidgetConfig { Title = "Температура", Unit = "°C" });

        await repo.SaveAsync(dashboard);
        var loaded = await repo.LoadAllAsync();

        var single = Assert.Single(loaded);
        Assert.Equal(dashboard.Id, single.Id);
        Assert.Equal("Котельня", single.Name);
        var widget = Assert.IsType<GaugeWidgetConfig>(Assert.Single(single.Widgets));
        Assert.Equal("°C", widget.Unit);
    }

    [Fact]
    public async Task SavingTwice_OverwritesInsteadOfDuplicating()
    {
        var repo = new JsonDashboardRepository(_root);
        var dashboard = new DashboardConfig { Name = "Стара назва" };

        await repo.SaveAsync(dashboard);
        dashboard.Name = "Нова назва";
        await repo.SaveAsync(dashboard);

        var loaded = await repo.LoadAllAsync();
        var single = Assert.Single(loaded);
        Assert.Equal("Нова назва", single.Name);
    }

    [Fact]
    public async Task Delete_RemovesDashboard()
    {
        var repo = new JsonDashboardRepository(_root);
        var dashboard = new DashboardConfig { Name = "Тимчасовий" };
        await repo.SaveAsync(dashboard);

        await repo.DeleteAsync(dashboard.Id);

        Assert.Empty(await repo.LoadAllAsync());
    }

    [Fact]
    public async Task BrokenFile_IsSkipped_OtherDashboardsStillLoad()
    {
        var repo = new JsonDashboardRepository(_root);
        await repo.SaveAsync(new DashboardConfig { Name = "Справний" });
        File.WriteAllText(Path.Combine(_root, "dashboards", "broken.json"), "{ це не json");

        var loaded = await repo.LoadAllAsync();

        var single = Assert.Single(loaded);
        Assert.Equal("Справний", single.Name);
    }
}