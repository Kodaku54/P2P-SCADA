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
using CommunityToolkit.Mvvm.ComponentModel;
using MsBox.Avalonia;
using MsBox.Avalonia.Enums;

namespace AvaloniaApplication1.ViewModels;

public partial class DashboardViewModel : ViewModelBase
{
    private readonly AppConfig _appConfig;
    private readonly DataSimulator? _simulator;

    public DashboardConfig Config { get; }
    public ObservableCollection<WidgetViewModelBase> Widgets { get; }
    public string DashboardTitle => Config.Title;
    private Bitmap? _backgroundImage;
    public Bitmap? BackgroundImage
    {
        get => _backgroundImage;
        set => SetProperty(ref _backgroundImage, value);
    }
    public event Action? DevicesChanged;

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

        if (result.NewDevice != null || result.NewSensor != null)
            DevicesChanged?.Invoke();

        var widget = DashboardFactory.CreateWidget(_appConfig, result.Widget);
        if (widget == null) return;

        Widgets.Add(widget);

        // 3. У режимі симуляції новий тег одразу отримує дані
        if (_simulator != null)
            DashboardFactory.AddSimulatedSignal(_simulator, result.Widget);
    }
    public async Task EditWidgetAsync(WidgetViewModelBase widget, Window owner)
    {
        if (widget.Config == null) return;

        var dialogVm = new AddWidgetViewModel(_appConfig, widget.Config);
        var dialog = new AddWidgetWindow { DataContext = dialogVm };
        dialogVm.CloseRequested += () => dialog.Close();

        await dialog.ShowDialog(owner);

        if (dialogVm.Result == null) return; // скасовано

        // Діалог уже змінив WidgetConfig на місці
        ConfigManager.Save(_appConfig);

        // Найпростіше застосувати зміни: створити віджет заново на тому ж місці списку
        var fresh = DashboardFactory.CreateWidget(_appConfig, widget.Config);
        if (fresh == null) return;

        var index = Widgets.IndexOf(widget);
        widget.Dispose();
        Widgets[index] = fresh;
    }
    public async Task DeleteWidgetAsync(WidgetViewModelBase widget)
    {
        var name = widget.Config?.Title ?? "віджет";

        var box = MessageBoxManager.GetMessageBoxStandard(
            "Видалення віджета",
            $"Видалити віджет \"{name}\"?",
            ButtonEnum.YesNo,
            MsBox.Avalonia.Enums.Icon.Warning);

        if (await box.ShowAsync() != ButtonResult.Yes) return;

        if (widget.Config != null)
            Config.Widgets.Remove(widget.Config);

        ConfigManager.Save(_appConfig);

        Widgets.Remove(widget);
        widget.Dispose();
    }
    public void RemoveWidgetsOf(string deviceId, string sensor)
    {
        var live = Widgets
            .Where(w => w.Config?.DeviceId == deviceId && w.Config.Sensor == sensor)
            .ToList();

        foreach (var widget in live)
        {
            Widgets.Remove(widget);
            widget.Dispose();
        }

        // Також конфіги без живого віджета (наприклад, якщо віджет не вдалось створити)
        Config.Widgets.RemoveAll(w => w.DeviceId == deviceId && w.Sensor == sensor);
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
    
    [ObservableProperty] private bool _isEditMode;

// Записує позицію й розмір віджета в конфіг (викликається один раз після перетягування)
    public void SaveLayout(WidgetViewModelBase widget)
    {
        if (widget.Config == null) return;

        widget.Config.X = widget.X;
        widget.Config.Y = widget.Y;
        widget.Config.Width = widget.Width;
        widget.Config.Height = widget.Height;

        ConfigManager.Save(_appConfig);
    }
    // Назва й фон дашборду тим самим діалогом
    public async Task EditDashboardAsync(Window owner)
    {
        var oldBackground = Config.BackgroundImage;

        var dialogVm = new AddDashboardViewModel(Config);
        var dialog = new AddDashboardWindow { DataContext = dialogVm };
        dialogVm.CloseRequested += () => dialog.Close();

        await dialog.ShowDialog(owner);

        if (dialogVm.Result == null) return; // скасовано

        // Діалог уже змінив DashboardConfig на місці
        ConfigManager.Save(_appConfig);

        OnPropertyChanged(nameof(DashboardTitle)); // оновлює назву вкладки

        if (Config.BackgroundImage != oldBackground)
        {
            BackgroundImage = LoadBitmap(Config.BackgroundImage);

            // Старий файл видаляємо, якщо його більше ніхто не використовує
            ConfigManager.DeleteBackgroundIfUnused(_appConfig, oldBackground);
        }
    }
}