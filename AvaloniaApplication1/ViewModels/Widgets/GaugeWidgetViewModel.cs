using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using System;

namespace AvaloniaApplication1.ViewModels.Widgets;

public partial class GaugeWidgetViewModel : WidgetViewModelBase
{
    // Ідентифікатор, за яким віджет розуміє, що це "його" дані (наприклад, "BoilerTemp")
    public string DataSourceTag { get; }

    // Текстові поля
    [ObservableProperty] private string _title;
    [ObservableProperty] private string _unit;

    // Налаштування шкали
    [ObservableProperty] private double _minValue;
    [ObservableProperty] private double _maxValue;
    
    // Поточний стан
    [ObservableProperty] private double _value;
    [ObservableProperty] private double _sweepAngle;
    [ObservableProperty] private IBrush _widgetColor = Brushes.White;
    [ObservableProperty] private bool _isCritical;

    // Пороги для зміни кольору (можна винести в параметри конструктора)
    public double LowWarningThreshold { get; set; }
    public double HighCriticalThreshold { get; set; }

    public GaugeWidgetViewModel(string dataSourceTag, string title, string unit, double min, double max, double? 
        customLowWarning = null, double? customHighCritical = null)
    {
        DataSourceTag = dataSourceTag;
        Title = title;
        Unit = unit;
        MinValue = min;
        MaxValue = max;
        
        LowWarningThreshold = customLowWarning ?? (min + (max - min) * 0.4);
        HighCriticalThreshold = customHighCritical ?? (min + (max - min) * 0.8);
        
        Value = min; // Старт з мінімуму

        // Підписуємось на значення свого тега. Avalonia автоматично викличе OnValueChanged
        BindTag(DataSourceTag, tagValue => Value = tagValue.Value);
    }

    // Цей метод автоматично викликається Toolkit-ом, коли змінюється _value
    partial void OnValueChanged(double value)
    {
        // 1. Логіка кольорів
        if (value <= LowWarningThreshold) 
        {
            WidgetColor = Brushes.LightSkyBlue;
            IsCritical = false;
        }
        else if (value >= HighCriticalThreshold) 
        {
            WidgetColor = Brushes.Tomato;
            IsCritical = true; // Вмикає XAML-анімацію пульсації
        }
        else 
        {
            WidgetColor = Brushes.White;
            IsCritical = false;
        }
        
        // 2. Логіка малювання дуги
        if (MaxValue - MinValue == 0) return; // Захист від ділення на нуль
        
        double clampedValue = Math.Clamp(value, MinValue, MaxValue);
        double percent = (clampedValue - MinValue) / (MaxValue - MinValue);
        SweepAngle = percent * 360.0;
    }
}