using System.Collections.Generic;

namespace AvaloniaApplication1.Models;

public record TailscaleNode(string Id, string HostName, string? Ip, bool Online, string Os);

public record TailscaleStatus(string BackendState, IReadOnlyList<TailscaleNode> Nodes);