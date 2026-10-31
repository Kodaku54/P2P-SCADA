using System.Collections.ObjectModel;
using System.Threading.Tasks;
using AvaloniaApplication1.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AvaloniaApplication1.ViewModels;

public partial class Page1ViewModel : ViewModelBase
{
    public ObservableCollection<string> NodeLines { get; } = new();

    [ObservableProperty]
    private string _networkState = "";

    // Результат останньої дії (запуск служби, підключення)
    [ObservableProperty]
    private string _message = "";

    public Page1ViewModel()
    {
        NetworkStatusService.Default.Changed += RefreshNodes;
        RefreshNodes();
    }

    private void RefreshNodes()
    {
        var net = NetworkStatusService.Default;
        NetworkState = $"Tailscale: {net.StateText}";

        NodeLines.Clear();
        foreach (var n in net.Nodes)
            NodeLines.Add($"{(n.Online ? "●" : "○")} {n.HostName}  {n.Ip}  {n.Os}  [{n.Id}]");
    }

    [RelayCommand]
    private async Task StartServiceAsync()
    {
        var net = NetworkStatusService.Default;

        Message = "Запуск служби…";
        var result = await net.Tailscale.EnsureDaemonRunningAsync();
        Message = result.Message;
        await net.RefreshNowAsync();
    }

    [RelayCommand]
    private async Task ConnectAsync()
    {
        var net = NetworkStatusService.Default;

        Message = "Підключення…";
        var result = await net.Tailscale.ConnectAsync();
        Message = result.Message;
        await net.RefreshNowAsync();
    }

    [RelayCommand]
    private async Task DisconnectAsync()
    {
        var net = NetworkStatusService.Default;

        var result = await net.Tailscale.DisconnectAsync();
        Message = result.Message;
        await net.RefreshNowAsync();
    }
}