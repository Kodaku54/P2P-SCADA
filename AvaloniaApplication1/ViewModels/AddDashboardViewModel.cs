using System;
using System.Collections.Generic;
using System.IO;
using AvaloniaApplication1.Models;
using AvaloniaApplication1.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AvaloniaApplication1.ViewModels;

public partial class AddDashboardViewModel : ViewModelBase
{
    public IReadOnlyList<DeviceConfig> Devices { get; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CreateCommand))]
    private string _title = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CreateCommand))]
    private DeviceConfig? _selectedDevice;

    // Що показуємо користувачеві поруч з кнопкою вибору фону
    [ObservableProperty]
    private string _backgroundDisplay = "Без фону";

    [ObservableProperty]
    private string? _errorMessage;

    // Файл, обраний користувачем; null означає "без фону"
    private string? _backgroundSourcePath;

    // Заповнюється після "Створити"; null, якщо діалог скасовано
    public DashboardConfig? Result { get; private set; }

    // Вікно підписується на цю подію й закривається
    public event Action? CloseRequested;

    public AddDashboardViewModel(IReadOnlyList<DeviceConfig> devices)
    {
        Devices = devices;
        SelectedDevice = devices.Count > 0 ? devices[0] : null;
    }

    // Викликається з вікна після вибору файлу. Сам файл копіюється пізніше, при "Створити"
    public void SetBackgroundFile(string path)
    {
        _backgroundSourcePath = path;
        BackgroundDisplay = Path.GetFileName(path);
        ErrorMessage = null;
    }

    [RelayCommand]
    private void ClearBackground()
    {
        _backgroundSourcePath = null;
        BackgroundDisplay = "Без фону";
    }

    private bool CanCreate() => !string.IsNullOrWhiteSpace(Title) && SelectedDevice != null;

    [RelayCommand(CanExecute = nameof(CanCreate))]
    private void Create()
    {
        string? background = null;
        if (_backgroundSourcePath != null)
        {
            try
            {
                background = ConfigManager.ImportBackground(_backgroundSourcePath);
            }
            catch (Exception ex)
            {
                // Файл могли видалити чи перемістити між вибором і "Створити"
                ErrorMessage = $"Не вдалося скопіювати зображення: {ex.Message}";
                return;
            }
        }

        Result = new DashboardConfig
        {
            Title = Title.Trim(),
            DeviceId = SelectedDevice!.Id,
            BackgroundImage = background
        };
        CloseRequested?.Invoke();
    }

    [RelayCommand]
    private void Cancel() => CloseRequested?.Invoke();
}