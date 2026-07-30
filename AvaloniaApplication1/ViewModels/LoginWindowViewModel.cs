using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MsBox.Avalonia;
using MsBox.Avalonia.Enums;

namespace AvaloniaApplication1.ViewModels;

public partial class LoginWindowViewModel : ViewModelBase
{
    [ObservableProperty]
    private string _username;
    [ObservableProperty]
    private string _password;

    [RelayCommand]
    private async Task Login()
    {
        await ShowAlertAsync();
    }
    private async Task ShowAlertAsync()
    {
        
        var box = MessageBoxManager.GetMessageBoxStandard(
            "Ім'я/пароль", // Заголовок
            "Ваші ім'я та пароль:" + Username + Password, // Текст
            ButtonEnum.Ok, 
            Icon.Success);
        var result = await box.ShowAsync(); 
    }
}