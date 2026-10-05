using System;
using System.Collections.ObjectModel;
using System.IO;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using AvaloniaApplication1.ViewModels.Widgets;
using System.Collections.Generic;

namespace AvaloniaApplication1.ViewModels;

public partial class DashboardViewModel : ViewModelBase
{
    public ObservableCollection<WidgetViewModelBase> Widgets { get; }
    public string DeviceId { get; }
    public string DashboardTitle { get; }
    public Bitmap? BackgroundImage { get; set; }

    
    public DashboardViewModel(string deviceId, string title, string? backgroundImagePath,
        IEnumerable<WidgetViewModelBase> widgets)
    {
        DeviceId = deviceId;
        DashboardTitle = title;
        BackgroundImage = LoadBitmap(backgroundImagePath);
        Widgets = new ObservableCollection<WidgetViewModelBase>(widgets);
    }

    private static Bitmap? LoadBitmap(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return null;

        try
        {
            // Ресурс усередині програми: "avares://AvaloniaApplication1/Assets/scheme.png"
            if (path.StartsWith("avares://"))
                return new Bitmap(AssetLoader.Open(new Uri(path)));

            // Звичайний файл на диску: "/home/user/schemes/plant.png"
            if (File.Exists(path))
                return new Bitmap(path);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Dashboard] Не вдалося завантажити фон: {ex.Message}");
        }

        return null;
    }

    private double _timeStep = 0;
}