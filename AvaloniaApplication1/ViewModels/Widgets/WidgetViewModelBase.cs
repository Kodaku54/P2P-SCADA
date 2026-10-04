using System;
using AvaloniaApplication1.Models;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AvaloniaApplication1.ViewModels.Widgets;

public abstract partial class WidgetViewModelBase : ViewModelBase
{
    [ObservableProperty] private string _title = "Невідомий датчик";
    [ObservableProperty] private double _x;
    [ObservableProperty] private double _y;
    [ObservableProperty] private double _width = 400;
    [ObservableProperty] private double _height = 250;
    
    // Той самий Id, що й у збереженому конфігу
    public Guid Id { get; private set; } = Guid.NewGuid();

    // З якого датчика віджет бере дані (потрібно, щоб зберегти його назад у конфіг)
    public TagRef Source { get; private set; } = new();

    // Віджет -> конфіг, готовий до запису у файл
    public abstract WidgetConfig ToConfig();

    // Конфіг -> спільні поля віджета
    protected void LoadCommon(WidgetConfig config)
    {
        Id = config.Id;
        Title = config.Title;
        X = config.X;
        Y = config.Y;
        Width = config.Width;
        Height = config.Height;
        Source = new TagRef { DeviceId = config.Source.DeviceId, Tag = config.Source.Tag };
    }

    // Спільні поля віджета -> конфіг
    protected void SaveCommon(WidgetConfig config)
    {
        config.Id = Id;
        config.Title = Title;
        config.X = X;
        config.Y = Y;
        config.Width = Width;
        config.Height = Height;
        config.Source = new TagRef { DeviceId = Source.DeviceId, Tag = Source.Tag };
    }
}