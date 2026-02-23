using LeatherShopAPI.DTOs.Broadcast;

namespace LeatherShopAPI.Services.Interfaces;

public interface IBroadcastService
{
    Task<BroadcastResultDto> SendBroadcastAsync(BroadcastRequestDto dto);
    Task<BroadcastHistoryDto?> GetBroadcastStatusAsync(int broadcastId);
    Task<List<BroadcastHistoryDto>> GetHistoryAsync();
    Task<List<WhatsAppTemplate>> GetTemplatesAsync();
}
