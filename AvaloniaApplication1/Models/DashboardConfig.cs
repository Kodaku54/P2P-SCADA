using System;
using System.Collections.Generic;

namespace AvaloniaApplication1.Models;

public class DashboardConfig
{
    // Версія формату файлу. Якщо колись змінимо структуру, за цим числом
    // зрозуміємо, як читати старі файли.
    public int SchemaVersion { get; set; } = 1;

    // Унікальний і незмінний ідентифікатор дашборду (ім'я файлу теж буде {Id}.json).
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Name { get; set; } = "Новий дашборд";

    // Ім'я файлу фонової схеми, який лежить поруч із дашбордом. null означає "без фону".
    public string? BackgroundFile { get; set; }

    public List<WidgetConfig> Widgets { get; set; } = new();
}