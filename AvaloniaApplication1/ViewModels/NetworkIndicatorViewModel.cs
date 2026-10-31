using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Media;
using AvaloniaApplication1.Models;
using AvaloniaApplication1.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MsBox.Avalonia;
using MsBox.Avalonia.Enums;

namespace AvaloniaApplication1.ViewModels;

public partial class NetworkIndicatorViewModel : ViewModelBase
{
    private static readonly IBrush GoodBrush = Brushes.LimeGreen;
    private static readonly IBrush WarnBrush = Brushes.Orange;
    private static readonly IBrush BadBrush = Brushes.IndianRed;
    private static readonly IBrush NeutralBrush = Brushes.Gray;

    // Те, що бачить інтерфейс. Із цих полів генеруються властивості Text, ToolTipText, ...
    [ObservableProperty] private string _text = "";
    [ObservableProperty] private string _toolTipText = "";
    [ObservableProperty] private IBrush _statusBrush = NeutralBrush;
    [ObservableProperty] private bool _canLogout;

    private string? _busyText;                  // що відбувається зараз ("підключення…"); null, якщо нічого
    private string _lastError = "";
    private bool _isBusy;                       // виконується дія, нові дії ігноруємо
    private CancellationTokenSource? _loginCts; // існує лише поки чекаємо на вхід у браузері
    private TailscaleState _shownState = TailscaleState.Unknown;

    public NetworkIndicatorViewModel()
    {
        NetworkStatusService.Default.Changed += Update;
        Update();
    }

    // Запуск застосунку

    public async Task EnsureOnLaunchAsync()
    {
        if (_isBusy) return;

        _isBusy = true;
        SetBusy("запуск…");
        try
        {
            var result = await NetworkStatusService.Default.Tailscale.EnsureDaemonRunningAsync();
            if (!result.Ok) _lastError = result.Message;
        }
        finally
        {
            await EndBusyAsync();
        }
    }

    // Ліва кнопка

    // AllowConcurrentExecutions: кнопка лишається активною, поки команда виконується.
    // Без цього не можна було б клікнути ще раз, щоб скасувати очікування входу.
    [RelayCommand(AllowConcurrentExecutions = true)]
    private async Task ToggleAsync()
    {
        // Клік під час очікування входу скасовує його
        if (_loginCts != null)
        {
            _loginCts.Cancel();
            return;
        }

        if (_isBusy) return;

        var net = NetworkStatusService.Default;
        if (net.IsSimulated)
        {
            _lastError = "У режимі симулятора Tailscale недоступний";
            Update();
            return;
        }

        _isBusy = true;
        _lastError = "";
        try
        {
            if (net.State == TailscaleState.Running)
                await DisconnectCoreAsync();
            else
                await ConnectCoreAsync();
        }
        finally
        {
            await EndBusyAsync();
        }
    }

    private async Task DisconnectCoreAsync()
    {
        if (!await ConfirmAsync("Відключення", "Відключити Tailscale?"))
            return;

        SetBusy("відключення…");
        var result = await NetworkStatusService.Default.Tailscale.DisconnectAsync();
        if (!result.Ok) _lastError = result.Message;
    }

    private async Task ConnectCoreAsync()
    {
        var tailscale = NetworkStatusService.Default.Tailscale;

        // 1. Службу (і застосунок на Windows) запускаємо, якщо вони не працюють
        SetBusy("запуск…");
        var ensure = await tailscale.EnsureDaemonRunningAsync();
        if (!ensure.Ok)
        {
            _lastError = ensure.Message;
            return;
        }

        // 2. Одразу після запуску Tailscale ще "прокидається" (Starting). Чекаємо до 10 секунд.
        var status = await tailscale.GetStatusAsync();
        for (var i = 0; i < 10 && status.State == TailscaleState.Starting; i++)
        {
            await Task.Delay(1000);
            status = await tailscale.GetStatusAsync();
        }

        if (status.State == TailscaleState.Running)
            return;   // Tailscale підключився сам, "up" не потрібен

        // 3. Підключаємо. Якщо потрібен вхід, "tailscale up" відкриє браузер
        SetBusy(status.State == TailscaleState.NeedsLogin ? "очікування входу…" : "підключення…");

        _loginCts = new CancellationTokenSource();
        try
        {
            var result = await tailscale.ConnectAsync(_loginCts.Token);
            if (!result.Ok) _lastError = result.Message;
        }
        finally
        {
            _loginCts.Dispose();
            _loginCts = null;
        }
    }

    // Права кнопка (меню)

    [RelayCommand]
    private async Task RefreshAsync()
    {
        _lastError = "";
        await NetworkStatusService.Default.RefreshNowAsync();
        Update();
    }

    [RelayCommand]
    private async Task LogoutAsync()
    {
        if (_isBusy) return;

        _isBusy = true;
        _lastError = "";
        try
        {
            if (!await ConfirmAsync("Вихід з акаунту", "Вийти з акаунту Tailscale?"))
                return;

            SetBusy("вихід…");
            var result = await NetworkStatusService.Default.Tailscale.LogoutAsync();
            if (!result.Ok) _lastError = result.Message;
        }
        finally
        {
            await EndBusyAsync();
        }
    }

    // Допоміжне 

    private static async Task<bool> ConfirmAsync(string title, string text)
    {
        var box = MessageBoxManager.GetMessageBoxStandard(
            title, text, ButtonEnum.YesNo, MsBox.Avalonia.Enums.Icon.Warning);

        return await box.ShowAsync() == ButtonResult.Yes;
    }

    private void SetBusy(string text)
    {
        _busyText = text;
        Update();
    }

    // Завершення дії: спершу оновлюємо реальний стан, потім прибираємо "зайнято"
    private async Task EndBusyAsync()
    {
        await NetworkStatusService.Default.RefreshNowAsync();
        _busyText = null;
        _isBusy = false;
        Update();
    }

    // Перераховує все, що бачить користувач
    private void Update()
    {
        var net = NetworkStatusService.Default;

        // Стара помилка зникає, коли стан Tailscale змінився
        if (net.State != _shownState)
        {
            _shownState = net.State;
            _lastError = "";
        }

        Text = $"Tailscale: {_busyText ?? net.StateText}";   // ?? означає "якщо зліва null, береться те, що справа"
        StatusBrush = _busyText != null ? WarnBrush : BrushFor(net.State);
        CanLogout = net.State is TailscaleState.Running or TailscaleState.Stopped;
        ToolTipText = BuildToolTip(net);
    }

    private static IBrush BrushFor(TailscaleState state) => state switch
    {
        TailscaleState.Running => GoodBrush,
        TailscaleState.Starting or TailscaleState.NeedsLogin => WarnBrush,
        TailscaleState.DaemonStopped or TailscaleState.NotInstalled => BadBrush,
        _ => NeutralBrush
    };

    private string BuildToolTip(NetworkStatusService net)
    {
        var lines = new List<string>();
        lines.Add(HintFor(net.State));

        if (net.State == TailscaleState.Running)
        {
            var online = net.Nodes.Count(n => n.Online);
            lines.Add($"Вузлів онлайн: {online} з {net.Nodes.Count}");
        }

        if (_lastError.Length > 0)
            lines.Add("Помилка: " + _lastError);
        
        return string.Join("\n", lines);
    }

    private string HintFor(TailscaleState state)
    {
        if (_loginCts != null)
            return "Очікування входу в браузері. Клік скасує очікування.";

        return state switch
        {
            TailscaleState.Running => "Відключити?",
            TailscaleState.Starting => "Tailscale запускається…",
            TailscaleState.Stopped => "Запустити?",
            TailscaleState.NeedsLogin => "Потрібен вхід. Натисність для входу.",
            TailscaleState.DaemonStopped => "Службу зупинено. Запустити?",
            TailscaleState.NotInstalled => "Tailscale не встановлено.",
            _ => "Стан невідомий. Спробувати запустити?"
        };
    }
}