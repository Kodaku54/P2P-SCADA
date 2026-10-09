using System;
using System.Threading.Tasks;
using CliWrap;
using CliWrap.Buffered;
using System.Collections.Generic;
using System.Text.Json;
using AvaloniaApplication1.Models;

namespace AvaloniaApplication1.Services; // Замініть на ваш простір імен

public class TailscaleService
{
    public async Task<TailscaleStatus?> GetStatusAsync()
    {
        var json = await GetStatusJsonAsync();
        return ParseStatus(json);
    }
    public static TailscaleStatus? ParseStatus(string json)
{
    if (string.IsNullOrWhiteSpace(json) || !json.TrimStart().StartsWith('{'))
        return null;

    try
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        var backendState = GetString(root, "BackendState") ?? "Unknown";
        var nodes = new List<TailscaleNode>();

        // "Self" (цей комп'ютер) пропускаємо, беремо лише "Peer".
        // Ключ словника (nodekey:...) не використовуємо: це ключ шифрування, він змінюється.
        if (root.TryGetProperty("Peer", out var peers) && peers.ValueKind == JsonValueKind.Object)
        {
            foreach (var entry in peers.EnumerateObject())
            {
                var peer = entry.Value;

                var id = GetString(peer, "ID");
                if (string.IsNullOrEmpty(id)) continue;

                nodes.Add(new TailscaleNode(
                    id,
                    GetString(peer, "HostName") ?? id,
                    FirstIpv4(peer),
                    peer.TryGetProperty("Online", out var online) && online.ValueKind == JsonValueKind.True,
                    GetString(peer, "OS") ?? ""));
            }
        }

        nodes.Sort((a, b) => string.Compare(a.HostName, b.HostName, StringComparison.OrdinalIgnoreCase));
        return new TailscaleStatus(backendState, nodes);
    }
    catch (JsonException)
    {
        return null;
    }
}

private static string? GetString(JsonElement element, string name) =>
    element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
        ? value.GetString()
        : null;

private static string? FirstIpv4(JsonElement peer)
{
    if (!peer.TryGetProperty("TailscaleIPs", out var ips) || ips.ValueKind != JsonValueKind.Array)
        return null;

    foreach (var ip in ips.EnumerateArray())
    {
        if (ip.ValueKind != JsonValueKind.String) continue;

        var text = ip.GetString();
        if (text != null && !text.Contains(':')) return text; // IPv4 без двокрапок
    }

    return null;
}
    public async Task<string> GetStatusJsonAsync()
    {
        try
        {
            var result = await Cli.Wrap("tailscale")
                .WithArguments("status --json")
                .WithValidation(CommandResultValidation.None)
                .ExecuteBufferedAsync();

            if (result.ExitCode == 0)
            {
                return result.StandardOutput; // Повертаємо чистий JSON
            }
            
            return $"Помилка консолі: {result.StandardError}";
        }
        catch (Exception ex)
        {
            return $"Критична помилка: {ex.Message}";
        }
    }
    
    public async Task<bool> ConnectAsync()
    {
        try
        {
            var result = await Cli.Wrap("tailscale")
                .WithArguments("up")
                // Якщо потрібен Auth Key для автоматичного підключення без браузера:
                // .WithArguments("up --authkey=tskey-auth-ВАШ_КЛЮЧ")
                .ExecuteBufferedAsync();

            return result.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }
    
    public async Task<bool> DisconnectAsync()
    {
        try
        {
            var result = await Cli.Wrap("tailscale")
                .WithArguments("down")
                .ExecuteBufferedAsync();

            return result.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }
}