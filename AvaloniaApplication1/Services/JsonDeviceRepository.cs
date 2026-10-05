using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using AvaloniaApplication1.Models;

namespace AvaloniaApplication1.Services;

/// <summary>
/// Зберігає кожен пристрій в окремому файлі devices/{Id}.json.
/// Працює так само, як JsonDashboardRepository (пізніше їх можна об'єднати в один загальний клас).
/// </summary>
public class JsonDeviceRepository : IDeviceRepository
{
    private readonly string _folder;
    private readonly SemaphoreSlim _writeLock = new(1, 1);

    public JsonDeviceRepository(string? rootFolder = null)
    {
        rootFolder ??= Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "P2P-SCADA");

        _folder = Path.Combine(rootFolder, "devices");
        Directory.CreateDirectory(_folder);
    }

    private string PathFor(Guid id) => Path.Combine(_folder, $"{id}.json");

    public async Task<IReadOnlyList<DeviceConfig>> LoadAllAsync()
    {
        var result = new List<DeviceConfig>();

        foreach (var file in Directory.EnumerateFiles(_folder, "*.json"))
        {
            try
            {
                await using var stream = File.OpenRead(file);
                var device = await JsonSerializer.DeserializeAsync<DeviceConfig>(stream, ConfigJson.Options);
                if (device != null)
                    result.Add(device);
            }
            catch (Exception ex) when (ex is JsonException or IOException)
            {
                System.Diagnostics.Debug.WriteLine($"[Devices] Пропускаю {file}: {ex.Message}");
            }
        }

        return result;
    }

    public async Task SaveAsync(DeviceConfig device)
    {
        await _writeLock.WaitAsync();
        try
        {
            string path = PathFor(device.Id);
            string tempPath = path + ".tmp";

            await using (var stream = File.Create(tempPath))
            {
                await JsonSerializer.SerializeAsync(stream, device, ConfigJson.Options);
            }

            File.Move(tempPath, path, overwrite: true);
        }
        finally
        {
            _writeLock.Release();
        }
    }
}