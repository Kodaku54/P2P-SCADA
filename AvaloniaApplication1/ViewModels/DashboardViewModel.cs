using System.IO;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using AvaloniaApplication1.Services;
using AvaloniaApplication1.ViewModels.Widgets;
using CommunityToolkit.Mvvm.Messaging;

namespace AvaloniaApplication1.ViewModels;
using System;
using System.Collections.ObjectModel;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;


public partial class DashboardViewModel : ViewModelBase
{
    public ObservableCollection<Widgets.WidgetViewModelBase> Widgets { get; } = new();
    public string DeviceIp { get; }
    public string DashboardTitle { get; }
    public Bitmap? BackgroundImage { get; set; }

    private readonly DataSimulator _simulator; //Симулятор
    
    public DashboardViewModel(string ipAddress, string title, string? backgroundImagePath = null)
    {
        DeviceIp = ipAddress;
        DashboardTitle = title;
        BackgroundImage = LoadBitmap(backgroundImagePath);
        Widgets = new ObservableCollection<WidgetViewModelBase>();
        string tagPrefix = $"{ipAddress}_";
        // Обидва віджети слухають один і той самий тег
        // Widgets.Add(new GaugeWidgetViewModel("Boiler_1_Temp", "Температура", "°C", 30, 70)
        // {
        //     X = 200, Y = 200
        // });
        // // Widgets.Add(new ChartWidgetViewModel("Boiler_1_Temp1", "Графік котла", "°C", 300));
        // Widgets.Add(new ChartWidgetViewModel("100.96.134.108_Sensor1", "Датчик з Малинки", "Unit", 50)
        // {
        //     X = 1, Y = 1
        // });
        //Старт симулятору
        string chartTag = $"{ipAddress}_Sensor1";   // було "100.96.134.108_Sensor1" — поверніть для реальних даних
        string gaugeTag = $"{ipAddress}_Temp";

        Widgets.Add(new ChartWidgetViewModel(chartTag, "Датчик з Малинки", "Unit", 50)
        {
            X = 20, Y = 20, Scale = 0.7
        });
        Widgets.Add(new GaugeWidgetViewModel(gaugeTag, "Температура", "°C", 0, 100)
        {
            X = 450, Y = 20
        });

        _simulator = new DataSimulator(500)
            .Add(chartTag, 0, 100, periodSeconds: 30)
            .Add(gaugeTag, 0, 100, periodSeconds: 20);
        _simulator.Start();
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