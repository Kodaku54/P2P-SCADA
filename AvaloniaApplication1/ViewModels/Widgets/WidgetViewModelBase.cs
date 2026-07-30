using CommunityToolkit.Mvvm.ComponentModel;

namespace AvaloniaApplication1.ViewModels.Widgets;

public partial class WidgetViewModelBase : ViewModelBase
{
    [ObservableProperty]
    private string _title = "Невідомий датчик";
}