using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using AvaloniaApplication1.Models;
using CliWrap;
using CliWrap.Buffered;

namespace AvaloniaApplication1.Services;

public class TailscaleService
{
    private static readonly TimeSpan StatusTimeout = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan ElevatedTimeout = TimeSpan.FromSeconds(60); // час на введення пароля

    // Шлях до CLI визначається один раз при першому зверненні до класу
    private static readonly string CliPath = ResolveCliPath();

    // Не дає запустити службу двічі одночасно (старт застосунку + кнопка)
    private readonly SemaphoreSlim _startLock = new SemaphoreSlim(1, 1);

    // Статус

    public async Task<TailscaleStatus> GetStatusAsync()
    {
        try
        {
            var result = await RunAsync(CliPath, "status --json", StatusTimeout);

            // Спершу пробуємо розібрати JSON, навіть якщо код виходу ненульовий
            var parsed = ParseStatus(result.StandardOutput);
            if (parsed != null)
                return parsed;

            if (result.ExitCode == 0)
                return Empty(TailscaleState.Unknown);

            // Код ненульовий і JSON немає: дивимось на текст помилки
            var text = result.StandardOutput + result.StandardError;

            // "unexpected state: NoState" / "Tailscale is starting": служба працює, але ще не готова
            if (text.Contains("NoState") || text.Contains("is starting"))
                return Empty(TailscaleState.Starting);

            // "failed to connect to local tailscaled process; is the Tailscale service running?"
            if (text.Contains("failed to connect"))
                return Empty(TailscaleState.DaemonStopped);

            return Empty(TailscaleState.Unknown);
        }
        catch (Win32Exception)
        {
            // Файл tailscale не знайдено: програму не встановлено
            return Empty(TailscaleState.NotInstalled);
        }
        catch (OperationCanceledException)
        {
            return Empty(TailscaleState.Unknown); // CLI не відповів за 5 секунд
        }
        catch (Exception)
        {
            // Опитування йде в нескінченному циклі, тут не можна падати
            return Empty(TailscaleState.Unknown);
        }
    }

    private static TailscaleStatus Empty(TailscaleState state) =>
        new TailscaleStatus(state, Array.Empty<TailscaleNode>());

    // Запуск служби 

    // Перевіряє службу і запускає її, якщо вона зупинена
    public async Task<OperationResult> EnsureDaemonRunningAsync()
    {
        if (!await _startLock.WaitAsync(0))
            return new OperationResult(false, "Запуск уже виконується");

        try
        {
            var status = await GetStatusAsync();

            if (status.State == TailscaleState.NotInstalled)
                return new OperationResult(false, "Tailscale не встановлено");

            // Крок 1: якщо служба зупинена, запускаємо її
            if (status.State == TailscaleState.DaemonStopped)
            {
                var start = await StartDaemonAsync();
                if (!start.Ok) return start;

                // Служба стартує не миттєво: до 10 секунд чекаємо, поки CLI почне відповідати
                var responded = false;
                for (var i = 0; i < 10; i++)
                {
                    await Task.Delay(1000);
                    status = await GetStatusAsync();
                    if (status.State != TailscaleState.DaemonStopped)
                    {
                        responded = true;
                        break;
                    }
                }

                if (!responded)
                    return new OperationResult(false, "Команду виконано, але служба не відповідає");
            }

            // Крок 2: служба працює, тепер перевіряємо застосунок (лише Windows)
            var guiStarted = StartGuiIfNeeded();
            //var guiStarted = false;

            return new OperationResult(true, guiStarted ? "Застосунок Tailscale запущено" : "Служба працює");
        }
        finally
        {
            _startLock.Release();
        }
    }

    private static async Task<OperationResult> StartDaemonAsync()
    {
        try
        {
            if (OperatingSystem.IsLinux()) return await StartOnLinuxAsync();
            if (OperatingSystem.IsWindows()) return await StartOnWindowsAsync();

            return new OperationResult(false, "Ця платформа не підтримується");
        }
        catch (Win32Exception ex) when (ex.NativeErrorCode == 1223)
        {
            // 1223: користувач натиснув "Ні" у вікні UAC (Windows)
            return new OperationResult(false, "Запуск скасовано");
        }
        catch (Win32Exception ex)
        {
            // Наприклад, на Linux не встановлено pkexec
            return new OperationResult(false, $"Не вдалося виконати команду: {ex.Message}");
        }
        catch (OperationCanceledException)
        {
            return new OperationResult(false, "Час очікування вичерпано");
        }
    }

    private static async Task<OperationResult> StartOnLinuxAsync()
    {
        // pkexec показує графічне вікно з паролем (потрібен polkit-агент у сесії)
        var result = await RunAsync("pkexec", "systemctl start tailscaled", ElevatedTimeout);

        if (result.ExitCode == 0)
            return new OperationResult(true, "Службу запущено");

        // 126: вікно закрито, 127: невірний пароль / немає прав
        if (result.ExitCode is 126 or 127)
            return new OperationResult(false, "Запуск скасовано або пароль невірний");

        return new OperationResult(false, $"systemctl: {result.StandardError.Trim()}");
    }

    private static async Task<OperationResult> StartOnWindowsAsync()
    {
        // Спосіб 1: запускаємо застосунок Tailscale. Він сам піднімає службу, UAC не потрібен.
        // StartGuiIfNeeded повертає false, якщо застосунок уже працює або його не знайдено.
        if (StartGuiIfNeeded())
            return new OperationResult(true, "Застосунок Tailscale запущено");

        // Спосіб 2 (запасний): запустити службу напряму. Це потребує підтвердження UAC.
        // Потрапляємо сюди, якщо застосунок уже працює, але служба зупинена, або його не знайдено.
        var info = new ProcessStartInfo
        {
            FileName = "sc.exe",
            Arguments = "start Tailscale",
            Verb = "runas",
            UseShellExecute = true,
            WindowStyle = ProcessWindowStyle.Hidden
        };

        using var process = Process.Start(info);
        if (process == null)
            return new OperationResult(false, "Не вдалося запустити sc.exe");

        await process.WaitForExitAsync();

        // 0: запущено, 1056: служба вже працювала
        return process.ExitCode is 0 or 1056
            ? new OperationResult(true, "Службу запущено")
            : new OperationResult(false, $"sc.exe завершився з кодом {process.ExitCode}");
    }
    

    // Підключення

    // Підключення. Якщо потрібен вхід, "tailscale up" друкує посилання: відкриваємо його в браузері
// і чекаємо, поки користувач увійде (до 3 хвилин) або скасує очікування.
public async Task<OperationResult> ConnectAsync(CancellationToken ct = default)
{
    var output = new StringBuilder();
    var browserOpened = false;

    // Локальна функція: викликається для кожного рядка, який друкує "tailscale up".
    // Вона бачить змінні output і browserOpened із методу навколо (це називається замиканням).
    void OnLine(string line)
    {
        // Рядки приходять із фонового потоку, а stdout і stderr можуть писати одночасно
        lock (output)
        {
            output.AppendLine(line);
        }

        // Посилання для входу друкується окремим рядком. Попередження теж можуть містити
        // посилання, але всередині речення, тому беремо лише рядок, що ЗАЧИНАЄТЬСЯ з https://
        var text = line.Trim();
        if (!browserOpened && text.StartsWith("https://"))
        {
            browserOpened = true;
            OpenInBrowser(text);
        }
    }

    // Об'єднуємо два способи скасування: кнопка користувача (ct) і таймаут 3 хвилини
    using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
    cts.CancelAfter(TimeSpan.FromMinutes(3));

    try
    {
        var result = await Cli.Wrap(CliPath)
            .WithArguments("up")
            .WithValidation(CommandResultValidation.None)
            .WithStandardOutputPipe(PipeTarget.ToDelegate(OnLine))   // кожен рядок виводу йде в OnLine
            .WithStandardErrorPipe(PipeTarget.ToDelegate(OnLine))
            .ExecuteAsync(cts.Token);

        if (result.ExitCode == 0)
            return new OperationResult(true, "Підключено");

        string error;
        lock (output)
        {
            error = output.ToString().Trim();
        }
        return new OperationResult(false, error.Length > 0 ? error : $"Код виходу {result.ExitCode}");
    }
    catch (Win32Exception)
    {
        return new OperationResult(false, "Tailscale не встановлено");
    }
    catch (OperationCanceledException)
    {
        return new OperationResult(false,
            ct.IsCancellationRequested ? "Очікування скасовано" : "Час очікування входу вичерпано");
    }
}

public Task<OperationResult> LogoutAsync() => RunTailscaleAsync("logout", TimeSpan.FromSeconds(10));

private static void OpenInBrowser(string url)
{
    try
    {
        // UseShellExecute = true: система сама відкриває посилання у браузері за замовчуванням
        Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
    }
    catch (Exception)
    {
        // Браузер не відкрився: нічого страшного, користувач може виконати "tailscale up" вручну
    }
}

    public Task<OperationResult> DisconnectAsync() => RunTailscaleAsync("down", TimeSpan.FromSeconds(10));

    private static async Task<OperationResult> RunTailscaleAsync(string arguments, TimeSpan timeout)
    {
        try
        {
            var result = await RunAsync(CliPath, arguments, timeout);

            if (result.ExitCode == 0)
                return new OperationResult(true, "Готово");

            var error = result.StandardError.Trim();
            return new OperationResult(false, error.Length > 0 ? error : $"Код виходу {result.ExitCode}");
        }
        catch (Win32Exception)
        {
            return new OperationResult(false, "Tailscale не встановлено");
        }
        catch (OperationCanceledException)
        {
            // "tailscale up" чекає на вхід в акаунт через браузер
            return new OperationResult(false,
                "Час очікування вичерпано. Якщо потрібен вхід, виконайте 'tailscale up' у терміналі.");
        }
    }

    // Допоміжне

    // Запускає програму і чекає на завершення не довше timeout
    private static async Task<BufferedCommandResult> RunAsync(string program, string arguments, TimeSpan timeout)
    {
        using var cts = new CancellationTokenSource(timeout);

        return await Cli.Wrap(program)
            .WithArguments(arguments)
            .WithValidation(CommandResultValidation.None)   // ненульовий код виходу не кидає виняток
            .ExecuteBufferedAsync(cts.Token);
    }

    private static string ResolveCliPath()
    {
        // На Windows CLI не завжди є в PATH, тому спершу перевіряємо стандартне місце встановлення
        if (OperatingSystem.IsWindows())
        {
            var path = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Tailscale", "tailscale.exe");

            if (File.Exists(path))
                return path;
        }

        // Linux (і запасний варіант для Windows): шукаємо в PATH
        return "tailscale";
    }

    private static TailscaleState MapState(string backendState) => backendState switch
    {
        "Running" => TailscaleState.Running,
        "Starting" => TailscaleState.Starting,
        "Stopped" => TailscaleState.Stopped,
        "NeedsLogin" => TailscaleState.NeedsLogin,
        _ => TailscaleState.Unknown   // NoState, NeedsMachineAuth тощо
    };

    public static TailscaleStatus? ParseStatus(string json)
    {
        if (string.IsNullOrWhiteSpace(json) || !json.TrimStart().StartsWith('{'))
            return null;

        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            var state = MapState(GetString(root, "BackendState") ?? "");
            var nodes = new List<TailscaleNode>();

            // "Self" (цей комп'ютер) пропускаємо, беремо лише "Peer".
            // Ключ словника (nodekey:...) не використовуємо: це ключ шифрування, він змінюється.
            if (root.TryGetProperty("Peer", out var peers) && peers.ValueKind == JsonValueKind.Object)
            {
                foreach (var entry in peers.EnumerateObject())
                {
                    var peer = entry.Value;

                    var id = GetString(peer, "ID");
                    if (string.IsNullOrEmpty(id)) continue;

                    nodes.Add(new TailscaleNode(
                        id,
                        GetString(peer, "HostName") ?? id,
                        FirstIpv4(peer),
                        peer.TryGetProperty("Online", out var online) && online.ValueKind == JsonValueKind.True,
                        GetString(peer, "OS") ?? ""));
                }
            }

            nodes.Sort((a, b) => string.Compare(a.HostName, b.HostName, StringComparison.OrdinalIgnoreCase));
            return new TailscaleStatus(state, nodes);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string? GetString(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static string? FirstIpv4(JsonElement peer)
    {
        if (!peer.TryGetProperty("TailscaleIPs", out var ips) || ips.ValueKind != JsonValueKind.Array)
            return null;

        foreach (var ip in ips.EnumerateArray())
        {
            if (ip.ValueKind != JsonValueKind.String) continue;

            var text = ip.GetString();
            if (text != null && !text.Contains(':')) return text; // IPv4 без двокрапок
        }

        return null;
    }
    private static bool StartGuiIfNeeded()
    {
        Debug.WriteLine("[Tailscale] StartGui викликано");
        if (!OperatingSystem.IsWindows()) return false;

        // Ім'я процесу вказується без ".exe"
        if (Process.GetProcessesByName("tailscale-ipn").Length > 0)
            return false; // вже працює

        var path = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Tailscale", "tailscale-ipn.exe");

        if (!File.Exists(path)) return false;

        try
        {
            Debug.WriteLine("[Tailscale] StartGui викликано");
            // Права адміністратора не потрібні: це звичайний запуск, як з меню "Пуск"
            Process.Start(new ProcessStartInfo { FileName = path, UseShellExecute = true });
            return true;
        }
        catch (Win32Exception)
        {
            return false;
        }
    }
    // Linux: дозволяє поточному користувачу керувати Tailscale без root. Потрібно один раз.
    public static async Task<OperationResult> SetOperatorAsync()
    {
        if (!OperatingSystem.IsLinux())
            return new OperationResult(false, "Потрібно лише на Linux");

        try
        {
            // $USER тут не підійде: pkexec запускає команду від root, і там $USER був би "root".
            // Тому ім'я поточного користувача беремо в самому застосунку.
            var user = Environment.UserName;

            var result = await RunAsync("pkexec", $"{CliPath} set --operator={user}", ElevatedTimeout);

            if (result.ExitCode == 0)
                return new OperationResult(true, "Доступ налаштовано");

            if (result.ExitCode is 126 or 127)
                return new OperationResult(false, "Налаштування скасовано або пароль невірний");

            return new OperationResult(false, result.StandardError.Trim());
        }
        catch (Win32Exception ex)
        {
            return new OperationResult(false, $"Не вдалося виконати команду: {ex.Message}");
        }
        catch (OperationCanceledException)
        {
            return new OperationResult(false, "Час очікування вичерпано");
        }
    }
}