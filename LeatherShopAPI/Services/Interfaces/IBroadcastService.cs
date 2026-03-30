using System.Threading;
using LeatherShopAPI.DTOs.Broadcast;
using LeatherShopAPI.Models;
using LeatherShopAPI.Models.WhatsApp;

namespace LeatherShopAPI.Services.Interfaces;

public interface IBroadcastService
{
    Task<BroadcastResultDto> SendBroadcastAsync(BroadcastRequestDto dto, CancellationToken ct = default);
    Task<BroadcastHistoryDto?> GetBroadcastStatusAsync(int broadcastId, CancellationToken ct = default);
    Task<PaginatedResult<BroadcastHistoryDto>> GetHistoryAsync(int page = 1, int pageSize = 10, string? sortField = null, string? sortOrder = null, string? templateSearch = null, int? recipientsFilter = null, int? sentFilter = null, int? deliveredFilter = null, int? readFilter = null, int? failedFilter = null, string? dateSearch = null, CancellationToken ct = default);
    Task<List<WhatsAppTemplate>> GetTemplatesAsync(CancellationToken ct = default);
    Task<int> GetTotalSentCountAsync(CancellationToken ct = default);
    Task<PaginatedResult<BroadcastRecipientDto>> GetRecipientsAsync(int broadcastId, int page = 1, int pageSize = 20, string? statusFilter = null, CancellationToken ct = default);
    Task<BroadcastDeliverySummaryDto?> GetDeliverySummaryAsync(int broadcastId, CancellationToken ct = default);
}
