using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using AvaloniaApplication1.ViewModels;
using AvaloniaApplication1.ViewModels.Widgets;

namespace AvaloniaApplication1.Views;

public partial class DashboardView : UserControl
{
    private double _zoom = 1.0;
    private Point _menuPoint;
    private const double Grid = 10;          // крок прив'язки до сітки; 1 = без прив'язки
    private const double ResizeZone = 20;    // розмір «ручки» в правому нижньому куті
    private const double MinWidgetSize = 80;

    private WidgetViewModelBase? _dragWidget;
    private bool _resizing;
    private Point _dragStart;
    private double _startX, _startY, _startW, _startH;

    private static double Snap(double v) => Math.Round(v / Grid) * Grid;

// Шукає зовнішній контейнер віджета (прямий нащадок Canvas), а не внутрішні ContentPresenter
    private static ContentPresenter? FindWidgetContainer(object? source) =>
        (source as Visual)?.GetSelfAndVisualAncestors()
        .OfType<ContentPresenter>()
        .FirstOrDefault(c => c.GetVisualParent() is Canvas && c.DataContext is WidgetViewModelBase);

    private static bool InResizeZone(WidgetViewModelBase w, Point posInWidget) =>
        posInWidget.X > w.Width - ResizeZone && posInWidget.Y > w.Height - ResizeZone;

    public DashboardView()
    {
        InitializeComponent();
        Scroller.AddHandler(PointerWheelChangedEvent, OnWheel, RoutingStrategies.Tunnel);
        // Запам'ятовуємо точку правого кліку у координатах схеми (масштаб і прокрутка вже враховані)
        WidgetsHost.AddHandler(PointerPressedEvent, OnHostPointerPressed, RoutingStrategies.Tunnel);
        WidgetsHost.PointerMoved += OnHostPointerMoved;
        WidgetsHost.PointerCaptureLost += OnHostCaptureLost;
        WidgetsHost.PointerReleased += (_, e) => e.Pointer.Capture(null); // далі спрацює CaptureLost → збереження
    }
    private void OnHostPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        var point = e.GetCurrentPoint(WidgetsHost);

        if (point.Properties.IsRightButtonPressed)
        {
            _menuPoint = point.Position;
            return;
        }

        if (!point.Properties.IsLeftButtonPressed) return;
        if (DataContext is not DashboardViewModel { IsEditMode: true }) return;

        var container = FindWidgetContainer(e.Source);
        if (container?.DataContext is not WidgetViewModelBase widget) return;

        _dragWidget = widget;
        _resizing = InResizeZone(widget, e.GetPosition(container));
        _dragStart = point.Position;
        _startX = widget.X; _startY = widget.Y;
        _startW = widget.Width; _startH = widget.Height;

        e.Pointer.Capture(WidgetsHost);
        e.Handled = true; // графік/датчик не повинні реагувати на цей клік
    }
    private void OnHostPointerMoved(object? sender, PointerEventArgs e)
    {
        // Без перетягування лише міняємо курсор
        if (_dragWidget == null)
        {
            UpdateCursor(e);
            return;
        }

        var delta = e.GetPosition(WidgetsHost) - _dragStart;

        if (_resizing)
        {
            _dragWidget.Width = Math.Max(MinWidgetSize, Snap(_startW + delta.X));
            _dragWidget.Height = Math.Max(MinWidgetSize, Snap(_startH + delta.Y));
        }
        else
        {
            _dragWidget.X = Math.Max(0, Snap(_startX + delta.X));
            _dragWidget.Y = Math.Max(0, Snap(_startY + delta.Y));
        }
    }
    private void OnHostCaptureLost(object? sender, PointerCaptureLostEventArgs e)
    {
        if (_dragWidget == null) return;

        var w = _dragWidget;
        _dragWidget = null;

        // Зберігаємо лише якщо щось справді змінилось
        bool changed = w.X != _startX || w.Y != _startY || w.Width != _startW || w.Height != _startH;
        if (changed && DataContext is DashboardViewModel vm)
            vm.SaveLayout(w);
    }
    private void UpdateCursor(PointerEventArgs e)
    {
        if (DataContext is not DashboardViewModel { IsEditMode: true })
        {
            WidgetsHost.Cursor = null;
            return;
        }

        var container = FindWidgetContainer(e.Source);
        if (container?.DataContext is not WidgetViewModelBase w)
        {
            WidgetsHost.Cursor = null;
            return;
        }

        WidgetsHost.Cursor = new Cursor(InResizeZone(w, e.GetPosition(container))
            ? StandardCursorType.BottomRightCorner
            : StandardCursorType.SizeAll);
    }

    private async void AddWidget_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not DashboardViewModel vm) return;

        // Вікно, в якому лежить цей дашборд (головне або відкріплене)
        if (TopLevel.GetTopLevel(this) is not Window owner) return;

        await vm.AddWidgetAsync(_menuPoint.X, _menuPoint.Y, owner);
    }
    private void OnWheel(object? sender, PointerWheelEventArgs e)
    {
        if (!e.KeyModifiers.HasFlag(KeyModifiers.Control)) return;
        SetZoom(_zoom * (e.Delta.Y > 0 ? 1.1 : 1 / 1.1), e.GetPosition(Scroller));
        e.Handled = true;
    }
    private void SetZoom(double value, Point anchor)
    {
        var newZoom = Math.Clamp(value, 0.2, 3.0);
        if (Math.Abs(newZoom - _zoom) < 0.0001) return;

        // Точка схеми під якорем у координатах без масштабу
        var contentX = (Scroller.Offset.X + anchor.X) / _zoom;
        var contentY = (Scroller.Offset.Y + anchor.Y) / _zoom;

        _zoom = newZoom;
        ZoomHost.LayoutTransform = new ScaleTransform(_zoom, _zoom);

        // Розмір вмісту оновиться після проходу layout, тож зсув виставляємо трохи пізніше
        Dispatcher.UIThread.Post(() =>
        {
            Scroller.Offset = new Vector(contentX * _zoom - anchor.X,
                contentY * _zoom - anchor.Y);
        }, DispatcherPriority.Loaded);
    }
    private Point ViewportCenter => new(Scroller.Bounds.Width / 2, Scroller.Bounds.Height / 2);

    private void ZoomIn_Click(object? s, RoutedEventArgs e)    => SetZoom(_zoom * 1.1, ViewportCenter);
    private void ZoomOut_Click(object? s, RoutedEventArgs e)   => SetZoom(_zoom / 1.1, ViewportCenter);
    private void ZoomReset_Click(object? s, RoutedEventArgs e) => SetZoom(1.0, ViewportCenter);
}