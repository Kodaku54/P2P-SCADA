using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using AvaloniaApplication1.Models;
using AvaloniaApplication1.ViewModels;
using AvaloniaApplication1.ViewModels.Widgets;

namespace AvaloniaApplication1.Services;

public class DashboardStore
{
    private readonly IDashboardRepository _dashboardRepository;
    private readonly IDeviceRepository _deviceRepository;
    private readonly Dictionary<Guid, DeviceConfig> _devicesById = new();
    private readonly WidgetFactory _factory;

    public ObservableCollection<DashboardViewModel> Dashboards { get; } = new();
    public IReadOnlyCollection<DeviceConfig> Devices => _devicesById.Values;
    
    public DashboardStore(IDashboardRepository dashboardRepository, IDeviceRepository deviceRepository)
    {
        _dashboardRepository = dashboardRepository;
        _deviceRepository = deviceRepository;
        _factory = new WidgetFactory(ResolveTag);
    }
    
    // Тимчасово: перетворює "який пристрій + який тег" на рядок для Messenger.
    // Якщо пристрій видалили, не падаємо: віджет просто не отримуватиме даних.
    public string ResolveTag(TagRef tag) =>
        _devicesById.TryGetValue(tag.DeviceId, out var device)
            ? $"{device.IpAddress}_{tag.Tag}"
            : $"unknown-{tag.DeviceId}_{tag.Tag}";
    
    public async Task LoadAsync()
    {
        IReadOnlyList<DeviceConfig> devices = await _deviceRepository.LoadAllAsync();
        IReadOnlyList<DashboardConfig> dashboards = await _dashboardRepository.LoadAllAsync();

        // Перший запуск: на диску порожньо, тож створюємо тестові дані й одразу зберігаємо їх,
        // щоб наступного разу вони прочиталися з файлів з тими самими Id.
        if (devices.Count == 0 && dashboards.Count == 0)
        {
            var sampleDevices = SampleData.CreateDevices();
            var sampleDashboards = sampleDevices.Select(SampleData.CreateDashboard).ToList();

            foreach (var device in sampleDevices)
                await _deviceRepository.SaveAsync(device);
            foreach (var dashboard in sampleDashboards)
                await _dashboardRepository.SaveAsync(dashboard);

            devices = sampleDevices;
            dashboards = sampleDashboards;
        }

        foreach (var device in devices)
            _devicesById[device.Id] = device;

        foreach (var config in dashboards.OrderBy(d => d.Name))
            Dashboards.Add(new DashboardViewModel(config, _factory));
    }

    public async Task<DashboardViewModel> AddAsync()
    {
        var config = new DashboardConfig { Name = $"Новий дашборд {Dashboards.Count + 1}" };
        var dashboard = new DashboardViewModel(config, _factory);

        Dashboards.Add(dashboard);
        await _dashboardRepository.SaveAsync(config);
        return dashboard;
    }

    public Task SaveAsync(DashboardViewModel dashboard) =>
        _dashboardRepository.SaveAsync(dashboard.ToConfig());

    public async Task RemoveAsync(DashboardViewModel dashboard)
    {
        Dashboards.Remove(dashboard);
        await _dashboardRepository.DeleteAsync(dashboard.Id);
    }
    
}