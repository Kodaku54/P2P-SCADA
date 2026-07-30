using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AvaloniaApplication1.Views;
using System.Linq;

namespace AvaloniaApplication1.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    public ObservableCollection<DashboardViewModel> Dashboards { get; }

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

    public MainWindowViewModel()
    {
        Dashboards = new ObservableCollection<DashboardViewModel>();
        Dashboards.Add(new DashboardViewModel("100.85.42.12", "Лабораторія RPi 1"));
        Dashboards.Add(new DashboardViewModel("100.85.42.13", "Лабораторія 2 RPi 3"));
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
            CurrentPage = dashboardToDetach;
        };

        // 5. Відкриваємо вікно!
        detachedWindow.Show();
    }
}