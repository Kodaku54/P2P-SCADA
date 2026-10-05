using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using AvaloniaApplication1.Models;
using AvaloniaApplication1.ViewModels.Widgets;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AvaloniaApplication1.ViewModels;

public partial class DashboardViewModel : ViewModelBase
{
    public Guid Id { get; }

    // Назву залишаємо під старим іменем, щоб не міняти XAML (MainWindow, DashboardWindow).
    // Атрибут генерує публічну властивість DashboardTitle зі сповіщенням про зміну.
    [ObservableProperty] private string _dashboardTitle = string.Empty;

    public ObservableCollection<WidgetViewModelBase> Widgets { get; } = new();
    public Bitmap? BackgroundImage { get; set; }

    // Ім'я файлу фону зберігаємо окремо: навіть якщо картинку не вдалося завантажити,
    // при збереженні воно не має зникнути з конфігу.
    private readonly string? _backgroundFile;

    /// <param name="assetsFolder">Папка, у якій лежить фонова картинка (для звичайних файлів).</param>
    public DashboardViewModel(DashboardConfig config, WidgetFactory factory, string? assetsFolder = null)
    {
        Id = config.Id;
        DashboardTitle = config.Name;
        _backgroundFile = config.BackgroundFile;
        BackgroundImage = LoadBitmap(config.BackgroundFile, assetsFolder);

        foreach (var widgetConfig in config.Widgets)
            Widgets.Add(factory.Create(widgetConfig));
    }

    // Дашборд -> конфіг, готовий до запису у файл
    public DashboardConfig ToConfig() => new()
    {
        Id = Id,
        Name = DashboardTitle,
        BackgroundFile = _backgroundFile,
        Widgets = Widgets.Select(w => w.ToConfig()).ToList()
    };

    private static Bitmap? LoadBitmap(string? file, string? assetsFolder)
    {
        if (string.IsNullOrWhiteSpace(file)) return null;

        try
        {
            // Ресурс усередині програми: "avares://AvaloniaApplication1/Assets/scheme.jpeg"
            if (file.StartsWith("avares://"))
                return new Bitmap(AssetLoader.Open(new Uri(file)));

            // Файл на диску: або поруч із дашбордом (assetsFolder), або за повним шляхом
            string path = assetsFolder is null ? file : Path.Combine(assetsFolder, file);
            if (File.Exists(path))
                return new Bitmap(path);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Dashboard] Не вдалося завантажити фон: {ex.Message}");
        }

        return null;
    }
}