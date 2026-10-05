using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using AvaloniaApplication1.Models;

namespace AvaloniaApplication1.Services;

public static class ConfigManager
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
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
                DeviceId = "rpi-lab-1",
                BackgroundImage = "avares://AvaloniaApplication1/Assets/scheme.jpeg",
                Widgets = new List<WidgetConfig>
                {
                    new() { Type = "Chart", Sensor = "pressure",    Title = "Тиск",        Unit = "бар", X = 20,  Y = 20, MaxPoints = 50 },
                    new() { Type = "Gauge", Sensor = "boiler_temp", Title = "Температура", Unit = "°C",  X = 450, Y = 20, Min = 0, Max = 100 }
                }
            }
        }
    };
}