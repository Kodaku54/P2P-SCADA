using Avalonia.Controls;
using Avalonia.Interactivity;
using AvaloniaApplication1.ViewModels;

namespace AvaloniaApplication1.Views;

public partial class DevicesView : UserControl
{
    public DevicesView()
    {
        InitializeComponent();
    }

    // async void допустимий лише для обробників подій: інакше await у них не виконати
    private async void EditSensor_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not DevicesViewModel vm) return;
        if ((sender as Control)?.DataContext is not SensorRow row) return;   // рядок датчика, на кнопці якого клікнули
        if (TopLevel.GetTopLevel(this) is not Window owner) return;          // вікно, в якому стоїть сторінка

        await vm.EditSensorAsync(row, owner);
    }
}