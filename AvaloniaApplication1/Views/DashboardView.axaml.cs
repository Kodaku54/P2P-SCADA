using System;
using System.Collections.Generic;
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
    private const double MinZoom = 0.2;
    private const double MaxZoom = 5.0;
    private const double ZoomStep = 1.1;       // зміна масштабу за один крок колеса
    private const double ZoomSmoothing = 0.8;  // 0..1: частка шляху до цілі за кадр (1 = без анімації)

    private double _zoom = 1.0;         // поточний масштаб (змінюється в анімації)
    private double _targetZoom = 1.0;   // масштаб, до якого рухаємось
    private Point _anchorView;          // точка в області перегляду, навколо якої масштабуємо
    private Point _anchorContent;       // та сама точка у координатах схеми (без масштабу)
    private readonly DispatcherTimer _zoomTimer = new() { Interval = TimeSpan.FromMilliseconds(16) };
    private Point _menuPoint;
    private const double Grid = 5;          // крок прив'язки до сітки; 1 = без прив'язки
    private const double ResizeZone = 20;    // розмір «ручки» в правому нижньому куті

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
        _zoomTimer.Tick += OnZoomTick;
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
            var target = FindWidgetContainer(e.Source)?.DataContext as WidgetViewModelBase;
            WidgetsHost.ContextMenu = BuildContextMenu(target); // меню відкриється при відпусканні кнопки
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
    // Різне меню для віджета і для порожнього місця
    private ContextMenu BuildContextMenu(WidgetViewModelBase? widget)
    {
        if (DataContext is not DashboardViewModel vm) return new ContextMenu();

        var owner = TopLevel.GetTopLevel(this) as Window;
        var (x, y) = (_menuPoint.X, _menuPoint.Y);
        var items = new List<Control>();

        if (widget != null)
        {
            var settings = new MenuItem { Header = "Налаштування…" };
            settings.Click += async (_, _) =>
            {
                if (owner != null) await vm.EditWidgetAsync(widget, owner);
            };

            var delete = new MenuItem { Header = "Видалити" };
            delete.Click += async (_, _) => await vm.DeleteWidgetAsync(widget);

            items.Add(settings);
            items.Add(delete);
        }
        else
        {
            var add = new MenuItem { Header = "Додати віджет…" };
            add.Click += async (_, _) =>
            {
                if (owner != null) await vm.AddWidgetAsync(x, y, owner);
            };

            var settings = new MenuItem { Header = "Налаштування дашборду…" };
            settings.Click += async (_, _) =>
            {
                if (owner != null) await vm.EditDashboardAsync(owner);
            };

            items.Add(add);
            items.Add(settings);
        }

        items.Add(new Separator());

        var edit = new MenuItem
        {
            Header = "Режим редагування",
            ToggleType = MenuItemToggleType.CheckBox,
            IsChecked = vm.IsEditMode
        };
        edit.Click += (_, _) => vm.IsEditMode = !vm.IsEditMode;
        items.Add(edit);

        return new ContextMenu { ItemsSource = items };
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
            _dragWidget.Width = Math.Max(_dragWidget.MinWidgetWidth, Snap(_startW + delta.X));
            _dragWidget.Height = Math.Max(_dragWidget.MinWidgetHeight, Snap(_startH + delta.Y));
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
    
    private void OnWheel(object? sender, PointerWheelEventArgs e)
    {
        if (!e.KeyModifiers.HasFlag(KeyModifiers.Control)) return;

        // Рахуємо від цільового масштабу, а не від поточного: кілька швидких кроків колеса накопичуються
        SetZoom(_targetZoom * (e.Delta.Y > 0 ? ZoomStep : 1 / ZoomStep), e.GetPosition(Scroller));
        e.Handled = true;
    }
    // Задає новий цільовий масштаб; сама зміна відбувається плавно в OnZoomTick
    private void SetZoom(double value, Point anchor)
    {
        var newTarget = Math.Clamp(value, MinZoom, MaxZoom);
        if (Math.Abs(newTarget - _targetZoom) < 0.0001) return;

        // Запам'ятовуємо точку схеми під якорем: масштабування триматиме її на місці.
        // _zoom і Offset завжди актуальні, бо їх виставляємо синхронно (див. ApplyZoom)
        _anchorView = anchor;
        _anchorContent = new Point((Scroller.Offset.X + anchor.X) / _zoom,
            (Scroller.Offset.Y + anchor.Y) / _zoom);

        _targetZoom = newTarget;
        _zoomTimer.Start();
    }
    // Один кадр анімації: наближає масштаб до цілі
    private void OnZoomTick(object? sender, EventArgs e)
    {
        var diff = _targetZoom - _zoom;

        if (Math.Abs(diff) < 0.002)
        {
            ApplyZoom(_targetZoom);   // фінальний точний масштаб
            _zoomTimer.Stop();
            return;
        }

        ApplyZoom(_zoom + diff * ZoomSmoothing);
    }

    private void ApplyZoom(double zoom)
    {
        _zoom = zoom;
        ZoomHost.LayoutTransform = new ScaleTransform(zoom, zoom);

        // Негайно перерахувати розмір вмісту, щоб ScrollViewer прийняв новий Offset
        // до малювання кадру (раніше зсув виставлявся пізніше, звідси миготіння)
        Scroller.UpdateLayout();

        Scroller.Offset = new Vector(_anchorContent.X * zoom - _anchorView.X,
            _anchorContent.Y * zoom - _anchorView.Y);
    }

    private Point ViewportCenter => new(Scroller.Bounds.Width / 2, Scroller.Bounds.Height / 2);

    private void ZoomIn_Click(object? s, RoutedEventArgs e)    => SetZoom(_targetZoom * ZoomStep, ViewportCenter);
    private void ZoomOut_Click(object? s, RoutedEventArgs e)   => SetZoom(_targetZoom / ZoomStep, ViewportCenter);
    private void ZoomReset_Click(object? s, RoutedEventArgs e) => SetZoom(1.0, ViewportCenter);
}