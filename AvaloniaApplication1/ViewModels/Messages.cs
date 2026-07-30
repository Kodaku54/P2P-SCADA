namespace AvaloniaApplication1.ViewModels;

public static class Messages
{
    // Ваш поточний універсальний рекорд для числових віджетів (Gauge, Chart)
    public record SensorDataMessage(string TagId, double Value);

    // На майбутнє: для віджетів-індикаторів (LedWidget, ToggleSwitch)
    public record DeviceStateMessage(string TagId, bool IsActive);
    
    // На майбутнє: для виведення аварій у текстову консоль на дашборді
    public record AlarmMessage(string MessageText, string Severity);
}