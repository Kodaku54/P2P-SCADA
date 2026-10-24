using System;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AvaloniaApplication1.ViewModels.Widgets;

public partial class IndicatorWidgetViewModel : WidgetViewModelBase
{
    public const string DefaultOnText = "Увімкнено";
    public const string DefaultOffText = "Вимкнено";

    private static readonly IBrush OnBrush = new SolidColorBrush(Color.Parse("#4CAF50"));
    private static readonly IBrush OffBrush = new SolidColorBrush(Color.Parse("#7A7A7A"));

    private readonly string _onText;
    private readonly string _offText;

    [ObservableProperty] private string _stateText = "—";
    [ObservableProperty] private IBrush _stateBrush = OffBrush;

    public IndicatorWidgetViewModel(string tagId, string title, string? onText, string? offText)
    {
        Title = title;
        _onText = string.IsNullOrWhiteSpace(onText) ? DefaultOnText : onText;
        _offText = string.IsNullOrWhiteSpace(offText) ? DefaultOffText : offText;

        // Округлення потрібне, щоб симулятор (дробові значення 0..1) теж перемикав стани
        BindTag(tagId, tagValue =>
        {
            var on = Math.Round(tagValue.Value) != 0;
            StateText = on ? _onText : _offText;
            StateBrush = on ? OnBrush : OffBrush;
        });
    }
}