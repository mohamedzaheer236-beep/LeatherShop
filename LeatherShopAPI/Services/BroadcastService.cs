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

        var broadcast = new BroadcastMessage
        {
            MessageTemplate = dto.TemplateName,
            MessageBody = string.Join(", ", dto.Parameters ?? new List<string>()),
            TotalRecipients = recipients.Count,
            SentCount = 0,
            FailedCount = 0
        };
        _db.BroadcastMessages.Add(broadcast);
        await _db.SaveChangesAsync();

        // Enqueue the job to the background service via Channel<T>.
        // This is non-blocking and the background service picks it up
        // with proper concurrency, DI scoping, and graceful shutdown.
        await _channel.Writer.WriteAsync(new BroadcastJob(
            BroadcastId: broadcast.Id,
            Recipients: recipients,
            TemplateName: dto.TemplateName,
            LanguageCode: dto.LanguageCode,
            Parameters: dto.Parameters?.ToList(),
            ImageUrl: dto.ImageUrl
        ));

        return new BroadcastResultDto
        {
            Message = dto.PhoneNumbers?.Any() == true
                ? "Broadcast started to selected customers"
                : "Broadcast started to all subscribers",
            BroadcastId = broadcast.Id,
            TotalRecipients = recipients.Count
        };
    }

    public async Task<List<BroadcastHistoryDto>> GetHistoryAsync()
    {
        return await _db.BroadcastMessages
            .OrderByDescending(b => b.SentAt)
            .Take(20)
            .Select(b => new BroadcastHistoryDto
            {
                Id = b.Id,
                MessageTemplate = b.MessageTemplate,
                MessageBody = b.MessageBody,
                TotalRecipients = b.TotalRecipients,
                SentCount = b.SentCount,
                FailedCount = b.FailedCount,
                SentAt = b.SentAt
            })
            .ToListAsync();
    }

    public async Task<List<WhatsAppTemplate>> GetTemplatesAsync()
    {
        return await _whatsApp.GetApprovedTemplates();
    }
}
