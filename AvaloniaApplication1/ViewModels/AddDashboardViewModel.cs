using System;
using System.IO;
using AvaloniaApplication1.Models;
using AvaloniaApplication1.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AvaloniaApplication1.ViewModels;

public partial class AddDashboardViewModel : ViewModelBase
{
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CreateCommand))]
    private string _title = string.Empty;

    // Що показуємо користувачеві поруч з кнопкою вибору фону
    [ObservableProperty]
    private string _backgroundDisplay = "Без фону";

    [ObservableProperty]
    private string? _errorMessage;

    // Файл, обраний користувачем; null означає "без фону"
    private string? _backgroundSourcePath;

    // Заповнюється після "Створити"; null, якщо діалог скасовано
    public DashboardConfig? Result { get; private set; }
    private readonly DashboardConfig? _existing;
    private bool _backgroundChanged;   // користувач обрав новий файл або скинув фон

    public bool IsEditing => _existing != null;
    public string WindowTitle => IsEditing ? "Налаштування дашборду" : "Новий дашборд";
    public string ActionText => IsEditing ? "Зберегти" : "Створити";

    // Вікно підписується на цю подію й закривається
    public event Action? CloseRequested;
    
    public AddDashboardViewModel() { }

// Режим редагування наявного дашборду
    public AddDashboardViewModel(DashboardConfig existing)
    {
        _existing = existing;
        Title = existing.Title;
        BackgroundDisplay = string.IsNullOrWhiteSpace(existing.BackgroundImage)
            ? "Без фону"
            : existing.BackgroundImage;
    }

    // Викликається з вікна після вибору файлу. Сам файл копіюється пізніше, при "Створити"
    public void SetBackgroundFile(string path)
    {
        _backgroundSourcePath = path;
        _backgroundChanged = true;
        BackgroundDisplay = Path.GetFileName(path);
        ErrorMessage = null;
    }

    [RelayCommand]
    private void ClearBackground()
    {
        _backgroundSourcePath = null;
        _backgroundChanged = true;
        BackgroundDisplay = "Без фону";
    }

    private bool CanCreate() => !string.IsNullOrWhiteSpace(Title);

    [RelayCommand(CanExecute = nameof(CanCreate))]
    private void Create()
    {
        // За замовчуванням фон лишається як був (для нового дашборду це null)
        string? background = _existing?.BackgroundImage;

        if (_backgroundChanged)
        {
            background = null;
            if (_backgroundSourcePath != null)
            {
                try
                {
                    background = ConfigManager.ImportBackground(_backgroundSourcePath);
                }
                catch (Exception ex)
                {
                    // Файл могли видалити чи перемістити між вибором і збереженням
                    ErrorMessage = $"Не вдалося скопіювати зображення: {ex.Message}";
                    return;
                }
            }
        }

        if (_existing != null)
        {
            // Редагування: змінюємо наявний конфіг на місці
            _existing.Title = Title.Trim();
            _existing.BackgroundImage = background;
            Result = _existing;
        }
        else
        {
            Result = new DashboardConfig
            {
                Title = Title.Trim(),
                BackgroundImage = background
            };
        }

        CloseRequested?.Invoke();
    }

    [RelayCommand]
    private void Cancel() => CloseRequested?.Invoke();
}