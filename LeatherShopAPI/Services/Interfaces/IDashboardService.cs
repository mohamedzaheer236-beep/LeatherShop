using System.Threading;
using LeatherShopAPI.DTOs.Dashboard;

namespace LeatherShopAPI.Services.Interfaces;

public interface IDashboardService
{
    Task<DashboardDto> GetDashboardAsync(DateTime? from = null, DateTime? to = null, CancellationToken ct = default);
}
