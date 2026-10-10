using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using AvaloniaApplication1.Models;
using AvaloniaApplication1.Services;
using AvaloniaApplication1.ViewModels.Widgets;
using AvaloniaApplication1.Views;

namespace AvaloniaApplication1.ViewModels;

public partial class DashboardViewModel : ViewModelBase
{
    private readonly AppConfig _appConfig;
    private readonly DataSimulator? _simulator;

    public DashboardConfig Config { get; }
    public ObservableCollection<WidgetViewModelBase> Widgets { get; }
    public string DashboardTitle => Config.Title;
    public Bitmap? BackgroundImage { get; set; }

    // Для підказки на порожньому дашборді
    public bool IsEmpty => Widgets.Count == 0;

    public DashboardViewModel(AppConfig appConfig, DashboardConfig config,
        IEnumerable<WidgetViewModelBase> widgets, DataSimulator? simulator)
    {
        _appConfig = appConfig;
        _simulator = simulator;
        Config = config;
        BackgroundImage = LoadBitmap(config.BackgroundImage);
        Widgets = new ObservableCollection<WidgetViewModelBase>(widgets);
        Widgets.CollectionChanged += (_, _) => OnPropertyChanged(nameof(IsEmpty));
    }

    public void Dispose()
    {
        foreach (var widget in Widgets)
            widget.Dispose();
    }

    // Відкриває вікно додавання віджета; x, y: точка на схемі, де натиснули праву кнопку
    public async Task AddWidgetAsync(double x, double y, Window owner)
    {
        var dialogVm = new AddWidgetViewModel(_appConfig, x, y);
        var dialog = new AddWidgetWindow { DataContext = dialogVm };
        dialogVm.CloseRequested += () => dialog.Close();

        await dialog.ShowDialog(owner);

        if (dialogVm.Result is not { } result) return; // скасовано

        // 1. Новий пристрій і датчик (якщо їх створили у вікні) потрапляють у конфіг
        if (result.NewDevice != null)
            _appConfig.Devices.Add(result.NewDevice);

        if (result.NewSensor != null)
        {
            var device = _appConfig.Devices.First(d => d.Id == result.Widget.DeviceId);
            device.Sensors.Add(result.NewSensor);
        }

        // 2. Віджет у конфіг і на екран
        Config.Widgets.Add(result.Widget);
        ConfigManager.Save(_appConfig);

        var widget = DashboardFactory.CreateWidget(_appConfig, result.Widget);
        if (widget == null) return;

        Widgets.Add(widget);

        // 3. У режимі симуляції новий тег одразу отримує дані
        if (_simulator != null)
            DashboardFactory.AddSimulatedSignal(_simulator, result.Widget);
    }

    private static Bitmap? LoadBitmap(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return null;

        try
        {
            // Ім'я файлу з папки Backgrounds або повний шлях
            var filePath = ConfigManager.ResolveBackgroundPath(path);
            if (File.Exists(filePath))
                return new Bitmap(filePath);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Dashboard] Не вдалося завантажити фон: {ex.Message}");
        }

        return null;
    }
}