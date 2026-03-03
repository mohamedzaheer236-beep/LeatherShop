using System.Threading;
using LeatherShopAPI.DTOs.Broadcast;
using LeatherShopAPI.Models;
using LeatherShopAPI.Models.WhatsApp;

namespace LeatherShopAPI.Services.Interfaces;

public interface IBroadcastService
{
    Task<BroadcastResultDto> SendBroadcastAsync(BroadcastRequestDto dto, CancellationToken ct = default);
    Task<BroadcastHistoryDto?> GetBroadcastStatusAsync(int broadcastId, CancellationToken ct = default);
    Task<PaginatedResult<BroadcastHistoryDto>> GetHistoryAsync(int page = 1, int pageSize = 10, CancellationToken ct = default);
    Task<List<WhatsAppTemplate>> GetTemplatesAsync(CancellationToken ct = default);
    Task<int> GetTotalSentCountAsync(CancellationToken ct = default);
}
