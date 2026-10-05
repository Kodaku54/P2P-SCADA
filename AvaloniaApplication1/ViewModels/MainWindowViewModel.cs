using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using AvaloniaApplication1.Services;
using AvaloniaApplication1.ViewModels.Widgets;
using AvaloniaApplication1.Views;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AvaloniaApplication1.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    private readonly DashboardStore _store;

    // ТИМЧАСОВО: симулятор живе тут, поки немає справжнього опитування пристроїв (крок 6)
    private readonly DataSimulator _simulator = new(500);

    // Колекція одна й та сама від початку, тому інтерфейс бачить дашборди, що з'являються після завантаження
    public ObservableCollection<DashboardViewModel> Dashboards => _store.Dashboards;

    // Може бути порожньою, якщо жодного дашборду не відкрито
    [ObservableProperty]
    private ViewModelBase? _currentPage;

    public MainWindowViewModel()
    {
        _store = new DashboardStore(new JsonDashboardRepository(), new JsonDeviceRepository());

        // У дизайнері Avalonia нічого не читаємо з диска і не запускаємо
        if (Design.IsDesignMode) return;

        _ = InitializeAsync();
    }

    private async Task InitializeAsync()
    {
        try
        {
            await _store.LoadAsync();

            // ТИМЧАСОВО: симулятор подає тестові дані на кожен віджет
            var tags = new HashSet<string>();
            foreach (var widget in _store.Dashboards.SelectMany(d => d.Widgets))
            {
                string tag = _store.ResolveTag(widget.Source);
                if (tags.Add(tag))   // один тег симулюємо один раз, інакше точки на графіку подвояться
                    _simulator.Add(tag, 0, 100, widget is ChartWidgetViewModel ? 30 : 20);
            }
            _simulator.Start();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Startup] Не вдалося завантажити дашборди: {ex.Message}");
        }
    }

    [RelayCommand]
    private void Login()
    {
        var window = new LoginWindow();
        window.Show();
    }

    [RelayCommand]
    private void GoToPage1()
    {
        CurrentPage = new Page1ViewModel();
    }

    [RelayCommand]
    private void NavigateToDevice(DashboardViewModel? selectedDashboard)
    {
        if (selectedDashboard != null)
        {
            CurrentPage = selectedDashboard;
        }
    }

    [RelayCommand]
    private async Task AddDashboard()
    {
        CurrentPage = await _store.AddAsync();
    }

    [RelayCommand]
    private async Task DeleteDashboard(DashboardViewModel? dashboard)
    {
        if (dashboard == null) return;

        await _store.RemoveAsync(dashboard);

        if (CurrentPage == dashboard)
        {
            CurrentPage = Dashboards.FirstOrDefault();
        }
    }

    [RelayCommand]
    private void DetachDashboard(DashboardViewModel? dashboardToDetach)
    {
        if (dashboardToDetach == null) return;

        // 1. Прибираємо дашборд зі списку вкладок (файл на диску залишається)
        Dashboards.Remove(dashboardToDetach);

        // 2. Якщо від'єднали дашборд, який зараз відкритий, переключаємо головне вікно на інший
        if (CurrentPage == dashboardToDetach)
        {
            CurrentPage = Dashboards.FirstOrDefault();
        }

        // 3. Створюємо нове фізичне вікно
        var detachedWindow = new DashboardWindow
        {
            DataContext = dashboardToDetach // Підсовуємо існуючий стан!
        };

        // 4. Коли користувач закриває вікно, повертаємо дашборд у список
        detachedWindow.Closed += (sender, args) =>
        {
            Dashboards.Add(dashboardToDetach);
        };

        // 5. Відкриваємо вікно
        detachedWindow.Show();
    }
}