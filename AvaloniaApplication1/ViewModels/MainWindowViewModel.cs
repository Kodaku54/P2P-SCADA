using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AvaloniaApplication1.Views;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using AvaloniaApplication1.Models;
using AvaloniaApplication1.Services;
using MsBox.Avalonia;
using MsBox.Avalonia.Enums;

namespace AvaloniaApplication1.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    public ObservableCollection<DashboardViewModel> Dashboards { get; }
    private readonly AppConfig _config;
    private readonly DataSimulator? _simulator;

    [RelayCommand]
    private void Login()
    {
        var window = new LoginWindow();
        window.Show();
    }    
    [ObservableProperty]
    private ViewModelBase _currentPage;
    [RelayCommand]
    private void GoToPage1()
    {
        CurrentPage = new Page1ViewModel();
    }
    
    [RelayCommand]
    private void NavigateToDevice(DashboardViewModel selectedDashboard)
    {
        if (selectedDashboard != null)
        {
            CurrentPage = selectedDashboard;
        }
    }
    [RelayCommand]
    private async Task AddDashboardAsync()
    {
        var owner = (Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.MainWindow;
        if (owner == null) return;

        var dialogVm = new AddDashboardViewModel(_config.Devices);
        var dialog = new AddDashboardWindow { DataContext = dialogVm };
        dialogVm.CloseRequested += () => dialog.Close();

        await dialog.ShowDialog(owner);

        if (dialogVm.Result is not { } dashboardConfig) return; // скасовано

        var dashboard = DashboardFactory.CreateDashboard(_config, dashboardConfig);
        if (dashboard == null) return;

        _config.Dashboards.Add(dashboardConfig);
        ConfigManager.Save(_config);

        Dashboards.Add(dashboard);
        CurrentPage = dashboard;
    }
    
    [RelayCommand]
    private async Task DeleteDashboardAsync(DashboardViewModel? dashboard)
    {
        if (dashboard == null) return;

        var box = MessageBoxManager.GetMessageBoxStandard(
            "Видалення дашборду",
            $"Видалити дашборд \"{dashboard.DashboardTitle}\"?\nЦю дію не можна скасувати.",
            ButtonEnum.YesNo,
            MsBox.Avalonia.Enums.Icon.Warning);

        if (await box.ShowAsync() != ButtonResult.Yes) return;

        // 1. Прибираємо з конфігу й зберігаємо
        _config.Dashboards.Remove(dashboard.Config);
        ConfigManager.Save(_config);
        ConfigManager.DeleteBackgroundIfUnused(_config, dashboard.Config.BackgroundImage);

        // 2. Прибираємо з інтерфейсу; якщо він був відкритий, перемикаємось на інший
        var wasCurrent = CurrentPage == dashboard;
        Dashboards.Remove(dashboard);
        if (wasCurrent)
            CurrentPage = Dashboards.FirstOrDefault();

        // 3. Відписуємо віджети від TagStore
        dashboard.Dispose();
    }

    public MainWindowViewModel()
    {
        _config = ConfigManager.Load();
        Dashboards = new ObservableCollection<DashboardViewModel>(DashboardFactory.Create(_config));

        _simulator = DashboardFactory.CreateSimulator(_config);
        _simulator?.Start();
    }
    [RelayCommand]
    private void DetachDashboard(DashboardViewModel dashboardToDetach)
    {
        if (dashboardToDetach == null) return;

        // 1. Видаляємо дашборд зі списку вкладок
        Dashboards.Remove(dashboardToDetach);

        // 2. Якщо ми від'єднали дашборд, який зараз відкритий, 
        // треба переключити головне вікно на щось інше (наприклад, на перший доступний або очистити)
        if (CurrentPage == dashboardToDetach)
        {
            CurrentPage = Dashboards.FirstOrDefault(); 
        }

        // 3. Створюємо нове фізичне вікно
        var detachedWindow = new DashboardWindow
        {
            DataContext = dashboardToDetach // Підсовуємо існуючий стан!
        };

        // 4. Підписуємось на подію закриття цього нового вікна
        detachedWindow.Closed += (sender, args) =>
        {
            // Коли користувач закриває вікно хрестиком, повертаємо дашборд у список
            Dashboards.Add(dashboardToDetach);
        
            // Робимо його знову активним у головному вікні (за бажанням)
            //CurrentPage = dashboardToDetach;
        };

        // 5. Відкриваємо вікно!
        detachedWindow.Show();
    }
}