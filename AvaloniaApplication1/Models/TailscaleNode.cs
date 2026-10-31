using System.Collections.Generic;

namespace AvaloniaApplication1.Models;

// Усі можливі стани Tailscale. Замість рядків "Running", "Stopped" тощо.
public enum TailscaleState
{
    Unknown,        // не вдалося визначити
    NotInstalled,   // CLI не знайдено
    DaemonStopped,  // CLI є, але служба не відповідає
    NeedsLogin,     // служба працює, потрібен вхід в акаунт
    Stopped,        // вхід виконано, але Tailscale вимкнено ("tailscale down")
    Starting,
    Running
}

public record TailscaleNode(string Id, string HostName, string? Ip, bool Online, string Os);

public record TailscaleStatus(TailscaleState State, IReadOnlyList<TailscaleNode> Nodes);

// Результат дії: успіх чи ні, і текст для користувача
public record OperationResult(bool Ok, string Message);