using System;
using System.Threading.Tasks;
using CliWrap;
using CliWrap.Buffered;

namespace AvaloniaApplication1.Services; // Замініть на ваш простір імен

public class TailscaleService
{
    public async Task<string> GetStatusJsonAsync()
    {
        try
        {
            var result = await Cli.Wrap("tailscale")
                .WithArguments("status --json")
                .WithValidation(CommandResultValidation.None)
                .ExecuteBufferedAsync();

            if (result.ExitCode == 0)
            {
                return result.StandardOutput; // Повертаємо чистий JSON
            }
            
            return $"Помилка консолі: {result.StandardError}";
        }
        catch (Exception ex)
        {
            return $"Критична помилка: {ex.Message}";
        }
    }
    
    public async Task<bool> ConnectAsync()
    {
        try
        {
            var result = await Cli.Wrap("tailscale")
                .WithArguments("up")
                // Якщо потрібен Auth Key для автоматичного підключення без браузера:
                // .WithArguments("up --authkey=tskey-auth-ВАШ_КЛЮЧ")
                .ExecuteBufferedAsync();

            return result.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }
    
    public async Task<bool> DisconnectAsync()
    {
        try
        {
            var result = await Cli.Wrap("tailscale")
                .WithArguments("down")
                .ExecuteBufferedAsync();

            return result.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }
}