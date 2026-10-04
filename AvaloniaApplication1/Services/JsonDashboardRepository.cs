using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using AvaloniaApplication1.Models;

namespace AvaloniaApplication1.Services;

public class JsonDashboardRepository : IDashboardRepository
{
    private readonly string _folder;

    // Щоб одночасні записи не заважали один одному (автозбереження може спрацювати двічі поспіль)
    private readonly SemaphoreSlim _writeLock = new(1, 1);

    // rootFolder можна передати з тесту (тимчасова папка).
    // Без аргументу береться стандартна папка даних користувача:
    //   Windows: C:\Users\<ви>\AppData\Roaming\P2P-SCADA\dashboards
    //   Linux:   ~/.config/P2P-SCADA/dashboards
    public JsonDashboardRepository(string? rootFolder = null)
    {
        rootFolder ??= Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "P2P-SCADA");

        _folder = Path.Combine(rootFolder, "dashboards");
        Directory.CreateDirectory(_folder);
    }

    // Ім'я файлу = Id, а не назва дашборду: перейменування не чіпає файл,
    // а в назві не буде символів, заборонених в іменах файлів.
    private string PathFor(Guid id) => Path.Combine(_folder, $"{id}.json");

    public async Task<IReadOnlyList<DashboardConfig>> LoadAllAsync()
    {
        var result = new List<DashboardConfig>();

        foreach (var file in Directory.EnumerateFiles(_folder, "*.json"))
        {
            try
            {
                await using var stream = File.OpenRead(file);
                var dashboard = await JsonSerializer.DeserializeAsync<DashboardConfig>(stream, ConfigJson.Options);
                if (dashboard != null)
                    result.Add(dashboard);
            }
            catch (Exception ex) when (ex is JsonException or IOException)
            {
                // Один зіпсований файл не повинен ламати запуск усієї програми
                System.Diagnostics.Debug.WriteLine($"[Dashboards] Skip {file}: {ex.Message}");
            }
        }

        return result;
    }

    public async Task SaveAsync(DashboardConfig dashboard)
    {
        await _writeLock.WaitAsync();
        try
        {
            string path = PathFor(dashboard.Id);
            string tempPath = path + ".tmp";

            // Спершу пишемо у тимчасовий файл...
            await using (var stream = File.Create(tempPath))
            {
                await JsonSerializer.SerializeAsync(stream, dashboard, ConfigJson.Options);
            }

            // ...і лише коли все записано, підміняємо ним справжній файл.
            File.Move(tempPath, path, overwrite: true);
        }
        finally
        {
            _writeLock.Release();
        }
    }

    public async Task DeleteAsync(Guid id)
    {
        await _writeLock.WaitAsync();
        try
        {
            string path = PathFor(id);
            if (File.Exists(path))
                File.Delete(path);
        }
        finally
        {
            _writeLock.Release();
        }
    }
}