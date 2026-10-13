using System;
using AvaloniaApplication1.Models;
using AvaloniaApplication1.Services;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AvaloniaApplication1.ViewModels.Widgets;

public partial class WidgetViewModelBase : ViewModelBase
{
    [ObservableProperty] private string _title = "Невідомий датчик";
    [ObservableProperty] private double _x;
    [ObservableProperty] private double _y;
    [ObservableProperty] private double _width = 400;
    [ObservableProperty] private double _height = 250;
    
    // true, якщо зв'язок з пристроєм втрачено і показане значення застаріле
    [ObservableProperty] private bool _isStale;
    public WidgetConfig? Config { get; set; }
    

    private IDisposable? _tagSubscription;

    // Прив'язує віджет до тега: onValue викликається в UI-потоці з кожним новим значенням
    // (а також одразу, якщо значення вже є в сховищі).
    protected void BindTag(string tagId, Action<TagValue> onValue)
    {
        _tagSubscription?.Dispose();
        _tagSubscription = TagStore.Default.Subscribe(tagId, tagValue =>
        {
            IsStale = tagValue.Quality == TagQuality.Stale;
            if (!IsStale) onValue(tagValue);
        });
    }

    public void Dispose()
    {
        _tagSubscription?.Dispose();
        _tagSubscription = null;
    }
}