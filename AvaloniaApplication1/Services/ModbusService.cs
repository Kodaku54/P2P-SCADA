using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using FluentModbus;
using AvaloniaApplication1.Models;
using AvaloniaApplication1.ViewModels;

namespace AvaloniaApplication1.Services;

public class ModbusService
{
    private readonly DeviceConfig _config;
    private readonly byte _unitId = 1; // Slave ID (можна теж винести в конфіг за потреби)

    // Конструктор приймає конфіг конкретного пристрою
    public ModbusService(DeviceConfig config)
    {
        _config = config;
    }

    public async Task StartPollingAsync(CancellationToken ct)
    {
        var endpoint = new IPEndPoint(IPAddress.Parse(_config.IpAddress), _config.Port);

        while (!ct.IsCancellationRequested)
        {
            using (var client = new ModbusTcpClient())
            {
                try
                {
                    System.Diagnostics.Debug.WriteLine($"[Modbus] Підключення до {_config.IpAddress}:{_config.Port}...");

                    // Таймаут на підключення (3 секунди)
                    using (var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(3)))
                    using (var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, timeoutCts.Token))
                    {
                        await Task.Run(() => client.Connect(endpoint, ModbusEndianness.BigEndian), linkedCts.Token);
                    }

                    client.ReadTimeout = 1000;
                    System.Diagnostics.Debug.WriteLine($"[Modbus] Підключено успішно до {_config.IpAddress}.");

                    // Внутрішній цикл: працює стабільно, поки є мережа
                    while (client.IsConnected && !ct.IsCancellationRequested)
                    {
                        // =========================================================
                        // ФАЗА 1: Швидке опитування всіх датчиків зі списку
                        // =========================================================
                        foreach (var sensor in _config.Sensors)
                        {
                            double finalValue = 0;

                            switch (sensor.DataType)
                            {
                                case "Bool":
                                    // Читаємо 1 Coil. Бібліотека поверне 1 байт.
                                    var coilData = await client.ReadCoilsAsync(_unitId, sensor.Address, 1, ct);
    
                                    // Робимо побітове "І" з одиницею. 
                                    // Якщо наймолодший біт дорівнює 1, то результат буде 1 (реле увімкнено).
                                    bool isCoilOn = (coilData.Span[0] & 1) == 1;
    
                                    // Тепер перетворюємо чистий bool у наш double для графіка
                                    finalValue = isCoilOn ? 1.0 : 0.0;
                                    break;

                                case "Short":
                                    var shortData = await client.ReadHoldingRegistersAsync<short>(_unitId, sensor.Address, 1, ct);
                                    finalValue = shortData.Span[0];
                                    break;

                                case "Float":
                                    var floatData = await client.ReadHoldingRegistersAsync<float>(_unitId, sensor.Address, 1, ct);
                                    finalValue = Math.Round(floatData.Span[0], 2);
                                    break;
                                    
                                default:
                                    continue; // Якщо тип невідомий - ігноруємо
                            }

                            // Відправляємо дані з унікальним тегом (наприклад "100.96.134.108_Boiler_Temp")
                            string messageTag = $"{_config.IpAddress}_{sensor.Tag}";
                            TagStore.Default.Update(messageTag, finalValue);

                            // МІКРО-ПАУЗА: Захист малинки від "флуду" запитами (10 мс)
                            await Task.Delay(10, ct);
                        }

                        // =========================================================
                        // ФАЗА 2: МАКРО-ПАУЗА (Загальний час оновлення для пристрою)
                        // =========================================================
                        await Task.Delay(_config.UpdateRateMs, ct);
                    }
                }
                catch (OperationCanceledException)
                {
                    // Ігноруємо - програма закривається
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[Modbus] Збій на {_config.IpAddress}: {ex.Message}");
                    try { if (client.IsConnected) client.Disconnect(); } catch { }
                }
            }

            // Пауза перед спробою реконекту (3 сек), якщо зв'язок обірвався
            await Task.Delay(3000, ct);
        }
    }
}