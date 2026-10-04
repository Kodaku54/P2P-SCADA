using System;
using AvaloniaApplication1.Models;

namespace AvaloniaApplication1.ViewModels.Widgets;

public class WidgetFactory
{
    // Тимчасово: перетворює посилання на датчик (DeviceId + Tag) у рядок для Messenger,
    // наприклад "100.85.42.12_Temp". Зникне, коли перейдемо на TagService.
    private readonly Func<TagRef, string> _resolveTag;

    public WidgetFactory(Func<TagRef, string> resolveTag)
    {
        _resolveTag = resolveTag;
    }

    public WidgetViewModelBase Create(WidgetConfig config) => config switch
    {
        GaugeWidgetConfig gauge => new GaugeWidgetViewModel(gauge, _resolveTag(gauge.Source)),
        ChartWidgetConfig chart => new ChartWidgetViewModel(chart, _resolveTag(chart.Source)),
        _ => throw new NotSupportedException($"Невідомий тип віджета: {config.GetType().Name}")
    };
}