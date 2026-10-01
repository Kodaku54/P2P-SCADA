using System;
using System.Threading.Tasks;
using AvaloniaApplication1.Services;
using CliWrap;
using CliWrap.Buffered;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AvaloniaApplication1.ViewModels;

public partial class Page1ViewModel : ViewModelBase
{
    private readonly TailscaleService _tailscale = new TailscaleService();
    [ObservableProperty]
    private string _tailscaleOutput = "Натисніть кнопку, щоб отримати статус...";

    [RelayCommand]
    private async Task GetTailscaleStatusAsync()
    {
        string rawJson = await _tailscale.GetStatusJsonAsync();
        TailscaleOutput = TryPrettyPrint(rawJson);
    }

    private static string TryPrettyPrint(string json)
    {
        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(json);
            return System.Text.Json.JsonSerializer.Serialize(doc, new System.Text.Json.JsonSerializerOptions
            {
                WriteIndented = true
            });
        }
        catch
        {
            return json; // якщо прийшла не-JSON помилка — покажемо як є
        }
    }
    [RelayCommand]
    private async Task StartTailscaleServiceAsync()
    {
        try
        {
            TailscaleOutput = "Очікування введення пароля для ЗАПУСКУ служби...";

            // 1. ЗАПУСК ДЕМОНА
            var startResult = await Cli.Wrap("pkexec")
                .WithArguments("systemctl start tailscaled")
                .WithValidation(CommandResultValidation.None)
                .ExecuteBufferedAsync();

            if (startResult.ExitCode != 0)
            {
                TailscaleOutput = "Дію скасовано або виникла помилка під час запуску.";
                return; // Виходимо, якщо користувач не ввів пароль
            }

            // 2. КОРИСНЕ НАВАНТАЖЕННЯ (Те, заради чого ми запускали службу)
            TailscaleOutput = "Служба працює! Виконуємо запит статусу...";
        
            var statusResult = await Cli.Wrap("tailscale")
                .WithArguments("status")
                .WithValidation(CommandResultValidation.None)
                .ExecuteBufferedAsync();
            
            TailscaleOutput = statusResult.StandardOutput;

            // Даємо користувачу 10 секунд подивитися на результат перед вбивством демона
            // У реальному додатку тут може бути очікування на натискання кнопки "Відключитися"
            await Task.Delay(10000); 
        }
        catch (Exception ex)
        {
            TailscaleOutput = $"Системна помилка: {ex.Message}";
        }
        finally
        {
            // 3. ГАРАНТОВАНЕ ВБИВСТВО ДЕМОНА
            // Цей блок виконається завжди, хай би що сталося вище.
            TailscaleOutput += "\n\n--- Знищення тунелю... ---";
        
            var stopResult = await Cli.Wrap("pkexec")
                .WithArguments("systemctl stop tailscaled")
                .WithValidation(CommandResultValidation.None)
                .ExecuteBufferedAsync();

            if (stopResult.ExitCode == 0)
            {
                TailscaleOutput += "\n✅ Службу Tailscale безпечно зупинено (Демон вбито).";
            }
            else
            {
                TailscaleOutput += $"\n❌ Помилка зупинки: {stopResult.StandardError}";
            }
        }
    }
}