using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;
using AvaloniaApplication1.Models;

namespace AvaloniaApplication1.Services;

/// <summary>
/// Періодично опитує Tailscale і тримає актуальний список вузлів.
/// Подія Changed викликається в UI-потоці й лише тоді, коли щось справді змінилося.
/// </summary>
public sealed class NetworkStatusService
{
    public static NetworkStatusService Default { get; } = new();

    private readonly TailscaleService _tailscale = new();
    private CancellationTokenSource? _cts;

    public IReadOnlyList<TailscaleNode> Nodes { get; private set; } = Array.Empty<TailscaleNode>();

    // "Running", "Stopped", "NeedsLogin"..., або "Unavailable", якщо CLI не відповів
    public string BackendState { get; private set; } = "Unknown";

    // true, якщо замість справжнього Tailscale працює заглушка
    public bool IsSimulated { get; private set; }

    public event Action? Changed;

    public void Start(TimeSpan interval, bool simulate = false)
    {
        if (_cts != null) return; // вже запущено

        IsSimulated = simulate;
        _cts = new CancellationTokenSource();
        _ = PollAsync(interval, simulate, _cts.Token);
    }

    public void Stop()
    {
        _cts?.Cancel();
        _cts = null;
    }

    private async Task PollAsync(TimeSpan interval, bool simulate, CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            var status = simulate ? SimulatedStatus() : await _tailscale.GetStatusAsync();
            Dispatcher.UIThread.Post(() => Apply(status));

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

    private void Apply(TailscaleStatus? status)
    {
        var state = status?.BackendState ?? "Unavailable";
        var nodes = status?.Nodes ?? Array.Empty<TailscaleNode>();

        if (state == BackendState && nodes.SequenceEqual(Nodes)) return;

        BackendState = state;
        Nodes = nodes;
        Changed?.Invoke();
    }
    
    private static TailscaleStatus SimulatedStatus() => new("Running", new[]
    {
        new TailscaleNode("rpi-lab-1", "rpi-lab-1", "100.64.0.1", true, "linux"),
        new TailscaleNode("rpi-lab-2", "rpi-lab-2", "100.64.0.2", false, "linux")
    });
}