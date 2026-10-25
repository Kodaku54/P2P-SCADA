using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AvaloniaApplication1.Models;

namespace AvaloniaApplication1.Services;

/// <summary>Тримає по одному GatewayClient на кожен пристрій із конфігу.</summary>
public sealed class DeviceManager
{
    public static DeviceManager Default { get; } = new();

    private readonly Dictionary<string, GatewayClient> _clients = new();
    private AppConfig? _config;

    /// <summary>Запускає клієнтів для всіх пристроїв. Викликати з UI-потоку.</summary>
    public void Start(AppConfig config)
    {
        _config = config;
        Sync();
    }

    /// <summary>Узгоджує клієнтів із конфігом і відправляє на Pi актуальні переліки датчиків.
    /// Викликати з UI-потоку після зміни пристроїв чи датчиків. До Start нічого не робить.</summary>
    public void Sync()
    {
        if (_config == null) return;

        // Нові пристрої
        foreach (var device in _config.Devices)
        {
            if (_clients.ContainsKey(device.Id)) continue;

            var client = new GatewayClient(device);
            _clients[device.Id] = client;
            client.Start();
        }

        // Видалені пристрої
        foreach (var id in _clients.Keys.Where(id => _config.Devices.All(d => d.Id != id)).ToList())
        {
            _clients[id].Dispose();
            _clients.Remove(id);
        }

        // Перелік датчиків міг змінитися
        foreach (var client in _clients.Values)
            _ = client.PushConfigAsync();
    }
    public Task<string?> WriteAsync(string deviceId, string tag, double value) =>
        _clients.TryGetValue(deviceId, out var client)
            ? client.WriteAsync(tag, value)
            : Task.FromResult<string?>("Пристрій не підключено.");
}