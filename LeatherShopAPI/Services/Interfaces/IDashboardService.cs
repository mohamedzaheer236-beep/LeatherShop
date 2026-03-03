using System.Threading;
using LeatherShopAPI.DTOs.Dashboard;

namespace LeatherShopAPI.Services.Interfaces;

public interface IDashboardService
{
    Task<DashboardDto> GetDashboardAsync(CancellationToken ct = default);
}
