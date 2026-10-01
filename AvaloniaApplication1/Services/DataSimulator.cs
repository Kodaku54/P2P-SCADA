using System;
using System.Collections.Generic;
using Avalonia.Threading;
using AvaloniaApplication1.ViewModels;
using CommunityToolkit.Mvvm.Messaging;

namespace AvaloniaApplication1.Services;

public sealed class DataSimulator
{
    private readonly DispatcherTimer _timer;
    private readonly List<(string Tag, double Min, double Max, double Period)> _signals = new();
    private readonly Random _rng = new();
    private double _t;

    public DataSimulator(double intervalMs = 500)
    {
        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(intervalMs) };
        _timer.Tick += OnTick;
    }

    // Додає сигнал: синусоїда в діапазоні [min, max] з періодом у секундах і невеликим шумом
    public DataSimulator Add(string tag, double min, double max, double periodSeconds = 20)
    {
        _signals.Add((tag, min, max, periodSeconds));
        return this;
    }

    public void Start() => _timer.Start();
    public void Stop() => _timer.Stop();

    private void OnTick(object? sender, EventArgs e)
    {
        _t += _timer.Interval.TotalSeconds;

        foreach (var (tag, min, max, period) in _signals)
        {
            double mid = (min + max) / 2;
            double amp = (max - min) / 2;

            double value = mid
                           + amp * 0.9 * Math.Sin(2 * Math.PI * _t / period)
                           + (_rng.NextDouble() - 0.5) * amp * 0.1;

            value = Math.Round(Math.Clamp(value, min, max), 2);

            WeakReferenceMessenger.Default.Send(new Messages.SensorDataMessage(tag, value));
        }
    }
}