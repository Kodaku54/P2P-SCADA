using System;
using System.Collections.Generic;
using AvaloniaApplication1.Models;
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

    [ObservableProperty]
    private string _backgroundImage = "avares://AvaloniaApplication1/Assets/scheme.jpeg";

    // Заповнюється після натискання "Створити"; null, якщо діалог скасовано
    public DashboardConfig? Result { get; private set; }

    // Вікно підписується на цю подію й закривається
    public event Action? CloseRequested;

    public AddDashboardViewModel(IReadOnlyList<DeviceConfig> devices)
    {
        Devices = devices;
        SelectedDevice = devices.Count > 0 ? devices[0] : null;
    }

    private bool CanCreate() => !string.IsNullOrWhiteSpace(Title) && SelectedDevice != null;

    [RelayCommand(CanExecute = nameof(CanCreate))]
    private void Create()
    {
        Result = new DashboardConfig
        {
            Title = Title.Trim(),
            DeviceId = SelectedDevice!.Id,
            BackgroundImage = string.IsNullOrWhiteSpace(BackgroundImage) ? null : BackgroundImage.Trim()
        };
        CloseRequested?.Invoke();
    }

    [RelayCommand]
    private void Cancel() => CloseRequested?.Invoke();
}