using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Threading;
using AvaloniaApplication1.ViewModels;

namespace AvaloniaApplication1.Views;

public partial class DashboardView : UserControl
{
    private double _zoom = 1.0;
    private Point _menuPoint;
    public DashboardView()
    {
        InitializeComponent();
        Scroller.AddHandler(PointerWheelChangedEvent, OnWheel, RoutingStrategies.Tunnel);
        // Запам'ятовуємо точку правого кліку у координатах схеми (масштаб і прокрутка вже враховані)
        WidgetsHost.AddHandler(PointerPressedEvent, OnHostPointerPressed, RoutingStrategies.Tunnel);
    }
    private void OnHostPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        var point = e.GetCurrentPoint(WidgetsHost);
        if (point.Properties.IsRightButtonPressed)
            _menuPoint = point.Position;
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