using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using LeatherShopAPI.Data;
using LeatherShopAPI.DTOs.Broadcast;
using LeatherShopAPI.Extensions;
using LeatherShopAPI.Models;
using LeatherShopAPI.Services.Interfaces;

namespace LeatherShopAPI.Services;

public class BroadcastService : IBroadcastService
{
    private readonly AppDbContext _db;
    private readonly IWhatsAppService _whatsApp;
    private readonly BroadcastChannel _channel;

    public BroadcastService(AppDbContext db, IWhatsAppService whatsApp, BroadcastChannel channel)
    {
        _db = db;
        _whatsApp = whatsApp;
        _channel = channel;
    }

    public async Task<BroadcastResultDto> SendBroadcastAsync(BroadcastRequestDto dto)
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
            recipients = await _db.Customers
                .Where(c => c.IsSubscribed)
                .Select(c => c.PhoneNumber)
                .ToListAsync();
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
        await _db.SaveChangesAsync();

        // Enqueue just the broadcast ID — background service reads all data from DB.
        // If app restarts before processing, ResumeIncompleteBroadcastsAsync picks it up.
        await _channel.Writer.WriteAsync(broadcast.Id);

        return new BroadcastResultDto
        {
            Message = dto.PhoneNumbers?.Any() == true
                ? "Broadcast started to selected customers"
                : "Broadcast started to all subscribers",
            BroadcastId = broadcast.Id,
            TotalRecipients = recipients.Count
        };
    }

    public async Task<BroadcastHistoryDto?> GetBroadcastStatusAsync(int broadcastId)
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
                FailedCount = b.FailedCount,
                SentAt = b.SentAt,
                Status = b.Status.ToString(),
                IsCarousel = b.IsCarousel
            })
            .FirstOrDefaultAsync();
    }

    public async Task<PaginatedResult<BroadcastHistoryDto>> GetHistoryAsync(int page = 1, int pageSize = 10)
    {
        var query = _db.BroadcastMessages.AsQueryable();

        var totalCount = await query.CountAsync();

        var items = await query
            .OrderByDescending(b => b.SentAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(b => new BroadcastHistoryDto
            {
                Id = b.Id,
                MessageTemplate = b.MessageTemplate,
                MessageBody = b.MessageBody,
                TotalRecipients = b.TotalRecipients,
                SentCount = b.SentCount,
                FailedCount = b.FailedCount,
                SentAt = b.SentAt,
                Status = b.Status.ToString(),
                IsCarousel = b.IsCarousel
            })
            .ToListAsync();

        return new PaginatedResult<BroadcastHistoryDto>
        {
            Items = items,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<List<WhatsAppTemplate>> GetTemplatesAsync()
    {
        return await _whatsApp.GetApprovedTemplates();
    }
}
