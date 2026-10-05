using System.Collections.Generic;
using System.Threading.Tasks;
using AvaloniaApplication1.Models;

namespace AvaloniaApplication1.Services;

public interface IDeviceRepository
{
    Task<IReadOnlyList<DeviceConfig>> LoadAllAsync();
    Task SaveAsync(DeviceConfig device);
}