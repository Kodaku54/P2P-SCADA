using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;
using AvaloniaApplication1.Models;

namespace AvaloniaApplication1.Services;

/// <summary>
/// З'єднання з шлюзом на одній Raspberry Pi: приймає значення в TagStore
/// і відправляє на Pi перелік датчиків пристрою.
/// </summary>
public sealed class GatewayClient : IDisposable
{
    public const int GatewayPort = 8765;
    private static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(3);
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(5) };

    private readonly DeviceConfig _device;
    private readonly CancellationTokenSource _cts = new();
    private volatile bool _connected;

    /// <summary>Постійний ID, який Pi повідомила в hello (поки не використовується)</summary>
    public string? PiId { get; private set; }

    public GatewayClient(DeviceConfig device) => _device = device;

    public void Start() => _ = RunAsync(_cts.Token);

    public void Dispose() => _cts.Cancel();

    // Tailscale знає актуальну адресу; IpAddress з конфігу лише запасний варіант
    private string? ResolveHost()
    {
        var node = NetworkStatusService.Default.Nodes.FirstOrDefault(n => n.Id == _device.Id);
        var host = node?.Ip ?? _device.IpAddress;
        return string.IsNullOrWhiteSpace(host) ? null : host;
    }

    private async Task RunAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                var host = ResolveHost();
                if (host != null)
                {
                    using var ws = new ClientWebSocket();
                    ws.Options.KeepAliveInterval = TimeSpan.FromSeconds(5);
                    ws.Options.KeepAliveTimeout = TimeSpan.FromSeconds(10); // виявляє "мовчазний" обрив

                    // Підключення не повинно висіти довго, якщо Pi вимкнена
                    using (var connectCts = CancellationTokenSource.CreateLinkedTokenSource(ct))
                    {
                        connectCts.CancelAfter(TimeSpan.FromSeconds(5));
                        await ws.ConnectAsync(new Uri($"ws://{host}:{GatewayPort}/ws"), connectCts.Token);
                    }

                    _connected = true;
                    Debug.WriteLine($"[Gateway] {_device.Id}: підключено до {host}");
                    await ReceiveLoopAsync(ws, ct);
                }
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Gateway] {_device.Id}: {ex.Message}");
            }
            finally
            {
                _connected = false;
                // Без зв'язку з Pi всі її значення застарілі
                TagStore.Default.MarkStale($"{_device.Id}/");
            }

            try { await Task.Delay(RetryDelay, ct); }
            catch (OperationCanceledException) { break; }
        }
    }

    private async Task ReceiveLoopAsync(ClientWebSocket ws, CancellationToken ct)
    {
        var buffer = new byte[16 * 1024];
        using var message = new MemoryStream();

        while (ws.State == WebSocketState.Open)
        {
            message.SetLength(0);

            // Одне JSON-повідомлення може прийти кількома шматками
            ValueWebSocketReceiveResult result;
            do
            {
                result = await ws.ReceiveAsync(buffer.AsMemory(), ct);
                if (result.MessageType == WebSocketMessageType.Close) return;
                message.Write(buffer, 0, result.Count);
            } while (!result.EndOfMessage);

            HandleMessage(message.GetBuffer().AsMemory(0, (int)message.Length));
        }
    }

    private void HandleMessage(ReadOnlyMemory<byte> json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            switch (root.GetProperty("type").GetString())
            {
                case "hello":
                    PiId = root.TryGetProperty("deviceId", out var id) ? id.GetString() : null;
                    // Застосунок власник переліку датчиків, тож відправляємо його одразу після підключення
                    _ = PushConfigAsync();
                    break;

                case "snapshot":
                case "update":
                    ApplyValues(root.GetProperty("values"));
                    break;

                // "status": значення з quality = bad приходять в update, окремо обробляти поки не треба
            }
        }
        catch (Exception ex)
        {
            // Одне зіпсоване повідомлення не повинно розривати з'єднання
            Debug.WriteLine($"[Gateway] {_device.Id}: не вдалося розібрати повідомлення: {ex.Message}");
        }
    }

    private void ApplyValues(JsonElement values)
    {
        foreach (var item in values.EnumerateArray())
        {
            var tag = item.GetProperty("tag").GetString();
            if (string.IsNullOrEmpty(tag)) continue;

            var tagId = DashboardFactory.TagId(_device.Id, tag);
            var good = item.GetProperty("quality").GetString() == "good";

            if (good && item.TryGetProperty("value", out var value) && value.ValueKind == JsonValueKind.Number)
            {
                DateTimeOffset? timestamp = item.TryGetProperty("ts", out var ts) && ts.ValueKind == JsonValueKind.Number
                    ? DateTimeOffset.FromUnixTimeMilliseconds(ts.GetInt64())
                    : null;

                TagStore.Default.Update(tagId, value.GetDouble(), timestamp);
            }
            else
            {
                TagStore.Default.MarkTagStale(tagId);
            }
        }
    }

    /// <summary>Відправляє на Pi актуальний перелік датчиків. Без з'єднання нічого не робить:
    /// після підключення перелік відправиться сам.</summary>
    public async Task PushConfigAsync()
    {
        if (!_connected) return;

        var host = ResolveHost();
        if (host == null) return;

        try
        {
            // Серіалізуємо в UI-потоці, бо перелік датчиків там же і змінюється
            var body = await Dispatcher.UIThread.InvokeAsync(() =>
                JsonSerializer.Serialize(new { Sensors = _device.Sensors, UpdateRateMs = _device.UpdateRateMs }));

            using var content = new StringContent(body, Encoding.UTF8, "application/json");
            var response = await Http.PutAsync($"http://{host}:{GatewayPort}/config", content);

            if (!response.IsSuccessStatusCode)
                Debug.WriteLine($"[Gateway] {_device.Id}: Pi відхилила конфігурацію: {await response.Content.ReadAsStringAsync()}");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[Gateway] {_device.Id}: не вдалося відправити конфігурацію: {ex.Message}");
        }
    }
    
    /// <summary>Записує значення в датчик. null означає успіх, інакше текст помилки.</summary>
    public async Task<string?> WriteAsync(string tag, double value)
    {
        var host = ResolveHost();
        if (host == null) return "Невідома адреса пристрою.";
        if (!_connected) return "Немає зв'язку з пристроєм.";

        try
        {
            var body = JsonSerializer.Serialize(new { value });
            using var content = new StringContent(body, Encoding.UTF8, "application/json");
            using var response = await Http.PutAsync(
                $"http://{host}:{GatewayPort}/sensors/{Uri.EscapeDataString(tag)}/value", content);

            if (response.IsSuccessStatusCode) return null;

            var text = await response.Content.ReadAsStringAsync();
            try
            {
                using var doc = JsonDocument.Parse(text);
                if (doc.RootElement.TryGetProperty("error", out var error))
                    return error.GetString();
            }
            catch (JsonException) { }

            return $"Помилка {(int)response.StatusCode}";
        }
        catch (Exception ex)
        {
            return ex.Message;
        }
    }
}