using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Unicode;
using AvaloniaApplication1.Models;
using System.Linq;

namespace AvaloniaApplication1.Services;

public static class ConfigManager
{
    public static string BackgroundsDirectory =>
        Path.Combine(Path.GetDirectoryName(ConfigPath)!, "Backgrounds");
    
    public static string ResolveBackgroundPath(string value) =>
        Path.IsPathRooted(value) ? value : Path.Combine(BackgroundsDirectory, value);
    
    public static string ImportBackground(string sourcePath)
    {
        Directory.CreateDirectory(BackgroundsDirectory);

        var name = Path.GetFileName(sourcePath);
        var baseName = Path.GetFileNameWithoutExtension(name);
        var extension = Path.GetExtension(name);
        var target = Path.Combine(BackgroundsDirectory, name);

        // Якщо файл з такою назвою вже є, додаємо суфікс, щоб нічого не перезаписати
        var counter = 1;
        while (File.Exists(target))
        {
            name = $"{baseName}_{counter++}{extension}";
            target = Path.Combine(BackgroundsDirectory, name);
        }

        File.Copy(sourcePath, target);
        return name;
    }
    
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        // Кирилицю й символи на кшталт "°" пишемо як є, а не як \uXXXX
        Encoder = JavaScriptEncoder.Create(UnicodeRanges.BasicLatin, UnicodeRanges.Cyrillic,
            UnicodeRanges.LatinExtendedA, UnicodeRanges.LatinExtendedB, UnicodeRanges.Latin1Supplement)
    };

    public static string ConfigPath { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "P2P-SCADA", "config.json");

    public static AppConfig Load()
    {
        if (!File.Exists(ConfigPath))
        {
            var created = CreateDefault();
            Save(created);
            return created;
        }

        try
        {
            var json = File.ReadAllText(ConfigPath);
            return JsonSerializer.Deserialize<AppConfig>(json, JsonOptions)
                   ?? throw new JsonException("Порожній конфіг");
        }
        catch (Exception ex)
        {
            // Зламаний файл не затираємо: відкладаємо вбік, щоб його можна було виправити
            Debug.WriteLine($"[Config] Не вдалося прочитати {ConfigPath}: {ex.Message}");
            try { File.Move(ConfigPath, ConfigPath + ".bad", overwrite: true); } catch { }

            var fallback = CreateDefault();
            Save(fallback);
            return fallback;
        }
    }

    public static void Save(AppConfig config)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(ConfigPath)!);

        // Пишемо в тимчасовий файл і підміняємо, щоб збій посеред запису не зіпсував конфіг
        var tmp = ConfigPath + ".tmp";
        File.WriteAllText(tmp, JsonSerializer.Serialize(config, JsonOptions));
        File.Move(tmp, ConfigPath, overwrite: true);
    }
    
    public static void DeleteBackgroundIfUnused(AppConfig config, string? name)
    {
        // Чіпаємо лише файли, якими керує застосунок: просте ім'я без шляху
        // (старі записи з "avares://..." чи повним шляхом на диску пропускаємо)
        if (string.IsNullOrWhiteSpace(name) || Path.GetFileName(name) != name) return;

        // Якщо інший дашборд використовує цей самий файл, лишаємо його
        if (config.Dashboards.Any(d => d.BackgroundImage == name)) return;

        try
        {
            var path = Path.Combine(BackgroundsDirectory, name);
            if (File.Exists(path))
                File.Delete(path);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[Config] Не вдалося видалити фон '{name}': {ex.Message}");
        }
    }

    // Шаблон для першого запуску
    private static AppConfig CreateDefault() => new()
    {
        UseSimulator = true,
        Devices = new List<DeviceConfig>
        {
            new()
            {
                Id = "rpi-lab-1", // замініть на Tailscale node ID вашої Pi
                Name = "Лабораторія RPi 1",
                Sensors = new List<SensorConfig>
                {
                    new() { Tag = "boiler_temp", Address = 0, DataType = "Float" },
                    new() { Tag = "pressure",    Address = 2, DataType = "Float" }
                }
            }
        },
        Dashboards = new List<DashboardConfig>
        {
            new()
            {
                Title = "Лабораторія RPi 1",
                Widgets = new List<WidgetConfig>
                {
                    new() { Type = "Chart", DeviceId = "rpi-lab-1", Sensor = "pressure",    Title = "Тиск",        Unit = "бар", X = 20,  Y = 20, MaxPoints = 50 },
                    new() { Type = "Gauge", DeviceId = "rpi-lab-1", Sensor = "boiler_temp", Title = "Температура", Unit = "°C",  X = 450, Y = 20, Min = 0, Max = 100 }
                }
            }
        }
    };
}