using System;
using System.IO;
using System.Text.Json;
using System.Collections.Generic;
using AvaloniaApplication1.Models;

namespace AvaloniaApplication1.Services;

public static class ConfigManager
{
    // Налаштування для красивого форматування JSON (щоб людям було зручно читати)
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true // Щоб не було помилок, якщо напишуть "tag" замість "Tag"
    };

    // Шлях до папки з конфігами (буде створена поруч із .exe файлом)
    private static readonly string _configDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Configs");

    /// <summary>
    /// Зчитує конфігурацію пристрою з файлу.
    /// </summary>
    public static DeviceConfig? LoadDeviceConfig(string fileName)
    {
        string filePath = Path.Combine(_configDirectory, fileName);

        if (!File.Exists(filePath))
        {
            System.Diagnostics.Debug.WriteLine($"[ConfigManager] Файл не знайдено: {filePath}");
            return null;
        }

        try
        {
            string json = File.ReadAllText(filePath);
            return JsonSerializer.Deserialize<DeviceConfig>(json, _jsonOptions);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ConfigManager] Помилка читання JSON: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Зберігає (або створює) конфігурацію пристрою у файл.
    /// Корисно для генерації шаблону!
    /// </summary>
    public static void SaveDeviceConfig(DeviceConfig config, string fileName)
    {
        if (!Directory.Exists(_configDirectory))
        {
            Directory.CreateDirectory(_configDirectory);
        }

        string filePath = Path.Combine(_configDirectory, fileName);
        string json = JsonSerializer.Serialize(config, _jsonOptions);
        
        File.WriteAllText(filePath, json);
        System.Diagnostics.Debug.WriteLine($"[ConfigManager] Конфігурацію збережено: {filePath}");
    }
}