using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Platform.Storage;
using AvaloniaApplication1.ViewModels;

namespace AvaloniaApplication1.Views;

public partial class AddDashboardWindow : Window
{
    public AddDashboardWindow()
    {
        InitializeComponent();
    }
    private async void ChooseBackground_Click(object? sender, RoutedEventArgs e)
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Оберіть зображення мнемосхеми",
            AllowMultiple = false,
            FileTypeFilter = new[] { FilePickerFileTypes.ImageAll }
        });

        var path = files.FirstOrDefault()?.TryGetLocalPath();
        if (path != null && DataContext is AddDashboardViewModel vm)
            vm.SetBackgroundFile(path);
    }
}