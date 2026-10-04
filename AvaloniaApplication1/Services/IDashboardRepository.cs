using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AvaloniaApplication1.Models;

namespace AvaloniaApplication1.Services;

public interface IDashboardRepository
{
    Task<IReadOnlyList<DashboardConfig>> LoadAllAsync();
    Task SaveAsync(DashboardConfig dashboard);
    Task DeleteAsync(Guid id);
}