using System.IO;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
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
    
    public DashboardViewModel(string ipAddress, string title, string? backgroundImagePath = null)
    {
        DeviceIp = ipAddress;
        DashboardTitle = title;
        BackgroundImage = LoadBitmap(backgroundImagePath);
        Widgets = new ObservableCollection<WidgetViewModelBase>();
        string tagPrefix = $"{ipAddress}_";
        // Обидва віджети слухають один і той самий тег
        Widgets.Add(new GaugeWidgetViewModel("Boiler_1_Temp", "Температура", "°C", 30, 70)
        {
            X = 200, Y = 200
        });
        // Widgets.Add(new ChartWidgetViewModel("Boiler_1_Temp1", "Графік котла", "°C", 300));
        Widgets.Add(new ChartWidgetViewModel("100.96.134.108_Sensor1", "Датчик з Малинки", "Unit", 50)
        {
            X = 1, Y = 1
        });

        // Запуск таймера, який викликає SimulateNewData кожну секунду (або частіше)
        /*var timer = new DispatcherTimer();
        timer.Interval = TimeSpan.FromMilliseconds(100); // 100 мс буде цілком достатньо для тесту
        timer.Tick += (s, e) => SimulateNewData();
        timer.Start();*/
        
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

        return null; // немає фону, але програма не падає
    }

    private double _timeStep = 0;

    /*private void SimulateNewData()
    {
        _timeStep += 0.2; 
        
        double sineValue = Math.Sin(_timeStep) * 15 + 50;
        sineValue = Math.Round(sineValue, 1);
        
        WeakReferenceMessenger.Default.Send(new Messages.SensorDataMessage("Boiler_1_Temp", sineValue));
        WeakReferenceMessenger.Default.Send(new Messages.SensorDataMessage("Boiler_1_Temp1", sineValue));
    }*/
}