using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using AvaloniaApplication1.Models;
using AvaloniaApplication1.Services;
using Xunit;

namespace AvaloniaApplication1.Tests;

public class DashboardStoreTests : IDisposable
{
    private readonly string _root =
        Path.Combine(Path.GetTempPath(), "p2p-scada-store-tests-" + Guid.NewGuid());

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }

    // Кожен виклик імітує окремий запуск програми над тією самою папкою даних
    private DashboardStore CreateStore() =>
        new(new JsonDashboardRepository(_root), new JsonDeviceRepository(_root));

    [Fact]
    public async Task FirstRun_CreatesSampleData()
    {
        var store = CreateStore();

        await store.LoadAsync();

        Assert.Equal(2, store.Devices.Count);
        Assert.Equal(2, store.Dashboards.Count);
    }

    [Fact]
    public async Task SecondRun_LoadsSameIds_InsteadOfCreatingNewOnes()
    {
        var first = CreateStore();
        await first.LoadAsync();
        var dashboardIds = first.Dashboards.Select(d => d.Id).OrderBy(x => x).ToList();
        var widgetIds = first.Dashboards.SelectMany(d => d.Widgets).Select(w => w.Id).OrderBy(x => x).ToList();

        var second = CreateStore();
        await second.LoadAsync();

        Assert.Equal(2, second.Dashboards.Count);   // не 4: тестові дані не створились вдруге
        Assert.Equal(dashboardIds, second.Dashboards.Select(d => d.Id).OrderBy(x => x).ToList());
        Assert.Equal(widgetIds, second.Dashboards.SelectMany(d => d.Widgets).Select(w => w.Id).OrderBy(x => x).ToList());
    }

    [Fact]
    public async Task AddAsync_DashboardSurvivesRestart()
    {
        var first = CreateStore();
        await first.LoadAsync();

        var added = await first.AddAsync();

        var second = CreateStore();
        await second.LoadAsync();
        Assert.Equal(3, second.Dashboards.Count);
        Assert.Contains(second.Dashboards, d => d.Id == added.Id);
    }

    [Fact]
    public async Task RemoveAsync_DashboardStaysDeletedAfterRestart()
    {
        var first = CreateStore();
        await first.LoadAsync();
        var toRemove = first.Dashboards[0];

        await first.RemoveAsync(toRemove);

        var second = CreateStore();
        await second.LoadAsync();
        Assert.Single(second.Dashboards);               // лишився один, а не нові тестові
        Assert.DoesNotContain(second.Dashboards, d => d.Id == toRemove.Id);
    }

    [Fact]
    public void ResolveTag_UnknownDevice_DoesNotThrow()
    {
        var store = CreateStore();

        string tag = store.ResolveTag(new TagRef { DeviceId = Guid.NewGuid(), Tag = "Temp" });

        Assert.Contains("Temp", tag);
    }
}