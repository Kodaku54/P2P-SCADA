using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;
using AvaloniaApplication1.Models;

namespace AvaloniaApplication1.Services;

/// <summary>
/// Періодично опитує Tailscale і тримає актуальний стан та список вузлів.
/// Подія Changed викликається в UI-потоці й лише тоді, коли щось справді змінилося.
/// </summary>
public sealed class NetworkStatusService
{
    public static NetworkStatusService Default { get; } = new();

    private CancellationTokenSource? _cts;

    // Сервіс, через який сторінки викликають дії (запуск, підключення)
    public TailscaleService Tailscale { get; } = new();

    public IReadOnlyList<TailscaleNode> Nodes { get; private set; } = Array.Empty<TailscaleNode>();

    public TailscaleState State { get; private set; } = TailscaleState.Unknown;

    // true, якщо замість справжнього Tailscale працює заглушка
    public bool IsSimulated { get; private set; }

    // Короткий текст стану для інтерфейсу
    public string StateText
    {
        get
        {
            var text = State switch
            {
                TailscaleState.Running => "підключено",
                TailscaleState.Starting => "підключення…",
                TailscaleState.Stopped => "відключено",
                TailscaleState.NeedsLogin => "потрібен вхід",
                TailscaleState.DaemonStopped => "службу зупинено",
                TailscaleState.NotInstalled => "не встановлено",
                _ => "невідомо"
            };
            return IsSimulated ? text + " (симуляція)" : text;
        }
    }

    public event Action? Changed;

    public void Start(TimeSpan interval, bool simulate = false)
    {
        if (_cts != null) return; // вже запущено

        IsSimulated = simulate;
        _cts = new CancellationTokenSource();
        _ = PollAsync(interval, _cts.Token);
    }

    public void Stop()
    {
        _cts?.Cancel();
        _cts = null;
    }

    // Опитує негайно, не чекаючи наступного циклу (після запуску служби, підключення тощо)
    public async Task RefreshNowAsync()
    {
        var status = IsSimulated ? SimulatedStatus() : await Tailscale.GetStatusAsync();
        Dispatcher.UIThread.Post(() => Apply(status));
    }

    private async Task PollAsync(TimeSpan interval, CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            await RefreshNowAsync();

            try
            {
                await Task.Delay(interval, ct);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private void Apply(TailscaleStatus status)
    {
        if (status.State == State && status.Nodes.SequenceEqual(Nodes)) return;

        State = status.State;
        Nodes = status.Nodes;
        Changed?.Invoke();
    }

    private static TailscaleStatus SimulatedStatus() => new(TailscaleState.Running, new[]
    {
        new TailscaleNode("rpi-lab-1", "rpi-lab-1", "100.64.0.1", true, "linux"),
        new TailscaleNode("rpi-lab-2", "rpi-lab-2", "100.64.0.2", false, "linux")
    });
}