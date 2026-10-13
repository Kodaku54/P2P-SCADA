using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using LiveChartsCore;
using LiveChartsCore.Defaults;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using SkiaSharp;

namespace AvaloniaApplication1.ViewModels.Widgets;

public partial class ChartWidgetViewModel : WidgetViewModelBase
{
    public string DataSourceTag { get; }

    [ObservableProperty] 
    private string _title;

    // Колекція серій (ліній) для графіка
    public ObservableCollection<ISeries> Series { get; set; }
    
    // Налаштування осей
    public Axis[] XAxes { get; set; }
    public Axis[] YAxes { get; set; }

    // Внутрішня колекція самих значень
    private readonly ObservableCollection<ObservableValue> _values;
    private readonly int _maxPoints; // Ліміт точок у пам'яті

    // Конструктор ТІЛЬКИ з параметрами даних (без візуалу)
    public ChartWidgetViewModel(
        string dataSourceTag, 
        string title, 
        string yAxisLabel = "", 
        int maxPoints = 50)
    {
        DataSourceTag = dataSourceTag;
        Title = title;
        _maxPoints = maxPoints;
        
        // Ініціалізуємо порожню колекцію значень
        _values = new ObservableCollection<ObservableValue>();

        // Налаштовуємо вигляд лінії (жорстко закодовано для єдиного стилю UI)
        Series = new ObservableCollection<ISeries>
        {
            new LineSeries<ObservableValue>
            {
                Values = _values,
                Fill = null, // Без заливки під лінією
                GeometrySize = 0, // Прибираємо кружечки на точках
                Stroke = new SolidColorPaint(SKColors.DodgerBlue) { StrokeThickness = 3 },
                LineSmoothness = 0.5 // Приємне згладжування
            }
        };

        // Налаштовуємо осі
        YAxes = new Axis[] { new Axis { Name = yAxisLabel, NameTextSize = 14 } };
        XAxes = new Axis[] { new Axis { IsVisible = false } }; // Ховаємо нижню вісь

        // Підписуємось на розсилку даних
        BindTag(DataSourceTag, tagValue =>
        {
            // Додаємо нове значення
            _values.Add(new ObservableValue(tagValue.Value));

            // Видаляємо найстаріше, щоб графік "рухався"
            if (_values.Count > _maxPoints)
            {
                _values.RemoveAt(0);
            }
        });
    }
}