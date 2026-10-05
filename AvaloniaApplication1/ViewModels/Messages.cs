namespace AvaloniaApplication1.ViewModels;

public static class Messages
{
    // На майбутнє: для виведення аварій у текстову консоль на дашборді
    public record AlarmMessage(string MessageText, string Severity);
}