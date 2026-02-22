using LeatherShopAPI.DTOs.Broadcast;

namespace LeatherShopAPI.Services.Interfaces;

public interface IBroadcastService
{
    Task<BroadcastResultDto> SendBroadcastAsync(BroadcastRequestDto dto);
    Task<List<BroadcastHistoryDto>> GetHistoryAsync();
    Task<List<WhatsAppTemplate>> GetTemplatesAsync();
}
