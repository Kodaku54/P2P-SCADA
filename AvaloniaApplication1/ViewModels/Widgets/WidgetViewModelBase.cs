using CommunityToolkit.Mvvm.ComponentModel;

namespace AvaloniaApplication1.ViewModels.Widgets;

public partial class WidgetViewModelBase : ViewModelBase
{
    [ObservableProperty] private string _title = "Невідомий датчик";
    [ObservableProperty] private double _x;
    [ObservableProperty] private double _y;
    [ObservableProperty] private double _width = 400;
    [ObservableProperty] private double _height = 250;
}