using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using LeatherShopAPI.Data;
using LeatherShopAPI.DTOs.Broadcast;
using LeatherShopAPI.Extensions;
using LeatherShopAPI.Models;
using LeatherShopAPI.Models.WhatsApp;
using LeatherShopAPI.Services.Interfaces;

namespace LeatherShopAPI.Services;

public class BroadcastService : IBroadcastService
{
    private readonly AppDbContext _db;
    private readonly IWhatsAppService _whatsApp;
    private readonly BroadcastChannel _channel;
    private readonly BroadcastRetryChannel _retryChannel;

    public BroadcastService(AppDbContext db, IWhatsAppService whatsApp, BroadcastChannel channel, BroadcastRetryChannel retryChannel)
    {
        _db = db;
        _whatsApp = whatsApp;
        _channel = channel;
        _retryChannel = retryChannel;
    }

    public async Task<BroadcastResultDto> SendBroadcastAsync(BroadcastRequestDto dto, CancellationToken ct = default)
    {
        List<string> recipients;

        if (dto.PhoneNumbers != null && dto.PhoneNumbers.Any())
        {
            recipients = dto.PhoneNumbers
                .Select(PhoneNumberHelper.Normalize)
                .Where(p => !string.IsNullOrEmpty(p))
                .Distinct()
                .ToList();
        }
        else
        {
            var query = _db.Customers.Where(c => c.IsSubscribed);
            if (!string.IsNullOrEmpty(dto.Category) && Enum.TryParse<CustomerCategory>(dto.Category, ignoreCase: true, out var cat))
                query = query.Where(c => c.Category == cat);

            recipients = await query
                .Select(c => c.PhoneNumber)
                .ToListAsync(ct);
        }

        if (!recipients.Any())
            throw new InvalidOperationException("No recipients found");

        // Validate carousel broadcasts have card data
        if (dto.IsCarousel && (dto.CarouselCards == null || dto.CarouselCards.Count == 0))
            throw new InvalidOperationException("Carousel broadcasts require at least one card.");

        if (dto.IsCarousel && dto.CarouselCards!.Any(c => string.IsNullOrWhiteSpace(c.ImageUrl)))
            throw new InvalidOperationException("All carousel cards must have an image.");

        // Store ALL job data in DB so broadcast survives Railway restarts.
        // Channel<int> carries just the ID as an immediate trigger.
        var broadcast = new BroadcastMessage
        {
            MessageTemplate = dto.TemplateName,
            MessageBody = dto.IsCarousel && dto.CarouselCards?.Any() == true
                ? $"Carousel: {dto.CarouselCards.Count} cards"
                : string.Join(", ", dto.Parameters ?? new List<string>()),
            TotalRecipients = recipients.Count,
            SentCount = 0,
            FailedCount = 0,
            Status = BroadcastStatus.Pending,
            LanguageCode = dto.LanguageCode,
            ParametersJson = dto.Parameters != null ? JsonSerializer.Serialize(dto.Parameters) : null,
            ImageUrl = dto.ImageUrl,
            RecipientsJson = JsonSerializer.Serialize(recipients),
            ProcessedPhonesJson = "[]",
            IsCarousel = dto.IsCarousel,
            CarouselCardsJson = dto.IsCarousel && dto.CarouselCards != null
                ? JsonSerializer.Serialize(dto.CarouselCards)
                : null
        };
        _db.BroadcastMessages.Add(broadcast);
        await _db.SaveChangesAsync(ct);

        // Enqueue just the broadcast ID - background service reads all data from DB.
        // If app restarts before processing, ResumeIncompleteBroadcastsAsync picks it up.
        await _channel.Writer.WriteAsync(broadcast.Id, ct);

        return new BroadcastResultDto
        {
            Message = dto.PhoneNumbers?.Any() == true
                ? "Broadcast started to selected customers"
                : "Broadcast started to all subscribers",
            BroadcastId = broadcast.Id,
            TotalRecipients = recipients.Count
        };
    }

    public async Task<BroadcastHistoryDto?> GetBroadcastStatusAsync(int broadcastId, CancellationToken ct = default)
    {
        return await _db.BroadcastMessages
            .Where(b => b.Id == broadcastId)
            .Select(b => new BroadcastHistoryDto
            {
                Id = b.Id,
                MessageTemplate = b.MessageTemplate,
                MessageBody = b.MessageBody,
                TotalRecipients = b.TotalRecipients,
                SentCount = b.SentCount,
                FailedCount = b.Recipients.Any()
                    ? b.Recipients.Count(r => r.Status == BroadcastDeliveryStatus.Failed)
                    : b.FailedCount,
                DeliveredCount = b.Recipients.Count(r => r.Status == BroadcastDeliveryStatus.Delivered || r.Status == BroadcastDeliveryStatus.Read),
                ReadCount = b.Recipients.Count(r => r.Status == BroadcastDeliveryStatus.Read),
                SentAt = b.SentAt,
                Status = b.Status.ToString(),
                IsCarousel = b.IsCarousel
            })
            .FirstOrDefaultAsync(ct);
    }

    public async Task<PaginatedResult<BroadcastHistoryDto>> GetHistoryAsync(int page = 1, int pageSize = 10, string? sortField = null, string? sortOrder = null, string? templateSearch = null, int? recipientsFilter = null, int? sentFilter = null, int? deliveredFilter = null, int? readFilter = null, int? failedFilter = null, string? dateSearch = null, CancellationToken ct = default)
    {
        var query = _db.BroadcastMessages.AsQueryable();

        // Pre-projection filters (columns that exist on BroadcastMessage)
        if (!string.IsNullOrWhiteSpace(templateSearch))
            query = query.Where(b => b.MessageTemplate.Contains(templateSearch));
        if (recipientsFilter.HasValue)
            query = query.Where(b => b.TotalRecipients == recipientsFilter.Value);
        if (sentFilter.HasValue)
            query = query.Where(b => b.SentCount == sentFilter.Value);

        // Date filter — pre-projection (SentAt is a real column)
        if (!string.IsNullOrWhiteSpace(dateSearch))
        {
            var ds = dateSearch.Trim();
            if (DateTime.TryParse(ds, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsedDate))
            {
                // Exact date range (ignores time/timezone issues)
                var startOfDay = parsedDate.Date;
                var endOfDay = startOfDay.AddDays(1);
                query = query.Where(b => b.SentAt >= startOfDay && b.SentAt < endOfDay);
            }
            else
            {
                // Partial text search (e.g. "Mar", "2026")
                query = query.Where(b =>
                    EF.Functions.ILike(AppDbContext.ToChar(b.SentAt, "DD Mon YYYY"), $"%{ds}%"));
            }
        }

        // Project first so we can filter on computed count columns
        var projected = query.Select(b => new BroadcastHistoryDto
            {
                Id = b.Id,
                MessageTemplate = b.MessageTemplate,
                MessageBody = b.MessageBody,
                TotalRecipients = b.TotalRecipients,
                SentCount = b.SentCount,
                FailedCount = b.Recipients.Any()
                    ? b.Recipients.Count(r => r.Status == BroadcastDeliveryStatus.Failed)
                    : b.FailedCount,
                DeliveredCount = b.Recipients.Count(r => r.Status == BroadcastDeliveryStatus.Delivered || r.Status == BroadcastDeliveryStatus.Read),
                ReadCount = b.Recipients.Count(r => r.Status == BroadcastDeliveryStatus.Read),
                SentAt = b.SentAt,
                Status = b.Status.ToString(),
                IsCarousel = b.IsCarousel
            });

        // Post-projection filters (computed count columns)
        if (deliveredFilter.HasValue)
            projected = projected.Where(b => b.DeliveredCount == deliveredFilter.Value);
        if (readFilter.HasValue)
            projected = projected.Where(b => b.ReadCount == readFilter.Value);
        if (failedFilter.HasValue)
            projected = projected.Where(b => b.FailedCount == failedFilter.Value);

        var totalCount = await projected.CountAsync(ct);

        var isDesc = string.Equals(sortOrder, "desc", StringComparison.OrdinalIgnoreCase);
        projected = (sortField?.ToLowerInvariant()) switch
        {
            "messagetemplate" => isDesc ? projected.OrderByDescending(b => b.MessageTemplate) : projected.OrderBy(b => b.MessageTemplate),
            "totalrecipients" => isDesc ? projected.OrderByDescending(b => b.TotalRecipients) : projected.OrderBy(b => b.TotalRecipients),
            "sentcount" => isDesc ? projected.OrderByDescending(b => b.SentCount) : projected.OrderBy(b => b.SentCount),
            "deliveredcount" => isDesc ? projected.OrderByDescending(b => b.DeliveredCount) : projected.OrderBy(b => b.DeliveredCount),
            "readcount" => isDesc ? projected.OrderByDescending(b => b.ReadCount) : projected.OrderBy(b => b.ReadCount),
            "failedcount" => isDesc ? projected.OrderByDescending(b => b.FailedCount) : projected.OrderBy(b => b.FailedCount),
            "sentat" => isDesc ? projected.OrderByDescending(b => b.SentAt) : projected.OrderBy(b => b.SentAt),
            _ => projected.OrderByDescending(b => b.SentAt)
        };

        var items = await projected
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return new PaginatedResult<BroadcastHistoryDto>
        {
            Items = items,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<List<WhatsAppTemplate>> GetTemplatesAsync(CancellationToken ct = default)
    {
        return await _whatsApp.GetApprovedTemplates(ct);
    }

    public async Task<int> GetTotalSentCountAsync(CancellationToken ct = default)
    {
        return await _db.BroadcastMessages.SumAsync(b => b.SentCount, ct);
    }

    public async Task<PaginatedResult<BroadcastRecipientDto>> GetRecipientsAsync(
        int broadcastId, int page = 1, int pageSize = 20, string? statusFilter = null, CancellationToken ct = default)
    {
        var query = _db.BroadcastRecipients
            .Where(r => r.BroadcastMessageId == broadcastId);

        if (!string.IsNullOrEmpty(statusFilter) && Enum.TryParse<BroadcastDeliveryStatus>(statusFilter, true, out var status))
            query = query.Where(r => r.Status == status);

        var totalCount = await query.CountAsync(ct);

        var rawItems = await query
            .OrderBy(r => r.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(r => new
            {
                r.Id,
                r.Phone,
                Name = _db.Customers
                    .Where(c => c.PhoneNumber == r.Phone)
                    .Select(c => c.Name)
                    .FirstOrDefault(),
                Status = r.Status.ToString(),
                r.ErrorDetail,
                r.CreatedAt,
                r.OriginalSentAt,
                r.SentAt,
                r.DeliveredAt,
                r.ReadAt,
                r.FailedAt,
                r.RetryCount,
                r.NextRetryAt,
                r.RetryHistoryJson
            })
            .ToListAsync(ct);

        var items = rawItems.Select(r => new BroadcastRecipientDto
        {
            Id = r.Id,
            Phone = r.Phone,
            Name = r.Name,
            Status = r.Status,
            ErrorDetail = r.ErrorDetail,
            CreatedAt = r.CreatedAt,
            OriginalSentAt = r.OriginalSentAt,
            SentAt = r.SentAt,
            DeliveredAt = r.DeliveredAt,
            ReadAt = r.ReadAt,
            FailedAt = r.FailedAt,
            RetryCount = r.RetryCount,
            NextRetryAt = r.NextRetryAt,
            RetryHistory = string.IsNullOrEmpty(r.RetryHistoryJson)
                ? null
                : JsonSerializer.Deserialize<List<RetryAttemptEntryDto>>(r.RetryHistoryJson)
        }).ToList();

        return new PaginatedResult<BroadcastRecipientDto>
        {
            Items = items,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<BroadcastDeliverySummaryDto?> GetDeliverySummaryAsync(int broadcastId, CancellationToken ct = default)
    {
        var exists = await _db.BroadcastMessages.AnyAsync(b => b.Id == broadcastId, ct);
        if (!exists) return null;

        var counts = await _db.BroadcastRecipients
            .Where(r => r.BroadcastMessageId == broadcastId)
            .GroupBy(r => r.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync(ct);

        var lookup = counts.ToDictionary(c => c.Status, c => c.Count);

        return new BroadcastDeliverySummaryDto
        {
            TotalRecipients = counts.Sum(c => c.Count),
            Queued = lookup.GetValueOrDefault(BroadcastDeliveryStatus.Queued),
            Sent = lookup.GetValueOrDefault(BroadcastDeliveryStatus.Sent),
            Delivered = lookup.GetValueOrDefault(BroadcastDeliveryStatus.Delivered),
            Read = lookup.GetValueOrDefault(BroadcastDeliveryStatus.Read),
            Failed = lookup.GetValueOrDefault(BroadcastDeliveryStatus.Failed),
            RetryScheduled = await _db.BroadcastRecipients
                .CountAsync(r => r.BroadcastMessageId == broadcastId
                                 && r.Status == BroadcastDeliveryStatus.Failed
                                 && r.NextRetryAt != null, ct),
            RetryableCount = await _db.BroadcastRecipients
                .CountAsync(r => r.BroadcastMessageId == broadcastId
                                 && r.Status == BroadcastDeliveryStatus.Failed
                                 && r.RetryCount < 3
                                 && r.ErrorDetail != null && r.ErrorDetail.Contains("131049"), ct)
        };
    }

    public async Task<BroadcastRetryResultDto> RetryFailedRecipientsAsync(int broadcastId, CancellationToken ct = default)
    {
        var broadcast = await _db.BroadcastMessages.FirstOrDefaultAsync(b => b.Id == broadcastId, ct);
        if (broadcast == null)
            throw new InvalidOperationException("Broadcast not found.");

        // Find all failed recipients for this broadcast that have error 131049 and haven't exhausted retries
        var now = DateTime.UtcNow;
        var failedRecipients = await _db.BroadcastRecipients
            .Where(r => r.BroadcastMessageId == broadcastId
                        && r.Status == BroadcastDeliveryStatus.Failed
                        && r.RetryCount < 3
                        && (r.ErrorDetail != null && r.ErrorDetail.Contains("131049")))
            .ToListAsync(ct);

        if (failedRecipients.Count == 0)
            return new BroadcastRetryResultDto
            {
                ScheduledCount = 0,
                Message = "No retryable recipients found (only error 131049 with less than 3 retries can be retried)."
            };

        foreach (var recipient in failedRecipients)
        {
            // Schedule immediate retry (NextRetryAt = now)
            recipient.NextRetryAt = now;
        }

        await _db.SaveChangesAsync(ct);

        // Wake up the retry background service immediately
        await _retryChannel.Writer.WriteAsync(true, ct);

        return new BroadcastRetryResultDto
        {
            ScheduledCount = failedRecipients.Count,
            Message = $"Scheduled {failedRecipients.Count} recipient(s) for immediate retry. Processing now."
        };
    }
}
