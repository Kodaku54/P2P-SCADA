using System;
using System.Collections.ObjectModel;
using System.IO;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using AvaloniaApplication1.ViewModels.Widgets;
using System.Collections.Generic;
using AvaloniaApplication1.Models;
using AvaloniaApplication1.Services;

namespace AvaloniaApplication1.ViewModels;

public partial class DashboardViewModel : ViewModelBase
{
    public DashboardConfig Config { get; }
    public ObservableCollection<WidgetViewModelBase> Widgets { get; }
    public string DashboardTitle => Config.Title;
    public Bitmap? BackgroundImage { get; set; }

    
    public DashboardViewModel(DashboardConfig config, IEnumerable<WidgetViewModelBase> widgets)
    {
        Config = config;
        BackgroundImage = LoadBitmap(config.BackgroundImage);
        Widgets = new ObservableCollection<WidgetViewModelBase>(widgets);
    }
    public void Dispose()
    {
        foreach (var widget in Widgets)
            widget.Dispose();
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

    private double _timeStep = 0;
}