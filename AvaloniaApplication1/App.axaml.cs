using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core.Plugins;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Markup.Xaml;
using AvaloniaApplication1.ViewModels;
using AvaloniaApplication1.Views;

namespace AvaloniaApplication1;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // Avoid duplicate validations from both Avalonia and the CommunityToolkit. 
            // More info: https://docs.avaloniaui.net/docs/guides/development-guides/data-validation#manage-validationplugins
            DisableAvaloniaDataAnnotationValidation();
            desktop.MainWindow = new MainWindow
            {
                DataContext = new MainWindowViewModel(),
            };
            
            /*/*для тесту!#1#
            // НАША НОВА ЛОГІКА ІНІЦІАЛІЗАЦІЇ
            string configFileName = "RPi_Laboratory_1.json";
        
            // Намагаємось завантажити конфіг
            var config = Services.ConfigManager.LoadDeviceConfig(configFileName);

            // Якщо файлу немає (перший запуск) - створюємо його з тестовими даними
            if (config == null)
            {
                config = new Models.DeviceConfig
                {
                    IpAddress = "100.96.134.108",
                    Port = 10502,
                    UpdateRateMs = 500,
                    Sensors = new System.Collections.Generic.List<Models.SensorConfig>
                    {
                        new Models.SensorConfig { Tag = "Boiler_Temp", Address = 0, DataType = "Float" },
                        new Models.SensorConfig { Tag = "Pump_Status", Address = 2, DataType = "Bool" }
                    }
                };
            
                // Зберігаємо шаблон на диск
                Services.ConfigManager.SaveDeviceConfig(config, configFileName);
            }

            // Запускаємо сервіс із завантаженим (або новоствореним) конфігом
            var modbusService = new Services.ModbusService(config);
            _ = System.Threading.Tasks.Task.Run(() => modbusService.StartPollingAsync(System.Threading.CancellationToken.None));*/
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void DisableAvaloniaDataAnnotationValidation()
    {
        // Get an array of plugins to remove
        var dataValidationPluginsToRemove =
            BindingPlugins.DataValidators.OfType<DataAnnotationsValidationPlugin>().ToArray();

        // remove each entry found
        foreach (var plugin in dataValidationPluginsToRemove)
        {
            BindingPlugins.DataValidators.Remove(plugin);
        }
    }
}