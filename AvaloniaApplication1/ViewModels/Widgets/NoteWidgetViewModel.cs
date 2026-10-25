using CommunityToolkit.Mvvm.ComponentModel;

namespace AvaloniaApplication1.ViewModels.Widgets;

// Нотатка на дашборді: статичний текст без прив'язки до тега
public partial class NoteWidgetViewModel : WidgetViewModelBase
{
    [ObservableProperty] private string _text;
    [ObservableProperty] private double _fontSize;

    public NoteWidgetViewModel(string text, double fontSize)
    {
        _text = text;
        _fontSize = fontSize;
    }
}