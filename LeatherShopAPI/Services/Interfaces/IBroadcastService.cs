using LeatherShopAPI.DTOs.Broadcast;
using LeatherShopAPI.Models;

namespace LeatherShopAPI.Services.Interfaces;

public interface IBroadcastService
{
    Task<BroadcastResultDto> SendBroadcastAsync(BroadcastRequestDto dto);
    Task<BroadcastHistoryDto?> GetBroadcastStatusAsync(int broadcastId);
    Task<PaginatedResult<BroadcastHistoryDto>> GetHistoryAsync(int page = 1, int pageSize = 10);
    Task<List<WhatsAppTemplate>> GetTemplatesAsync();
}
