using Microsoft.EntityFrameworkCore;
using LeatherShopAPI.Data;
using LeatherShopAPI.DTOs.Broadcast;
using LeatherShopAPI.Models;
using LeatherShopAPI.Services.Interfaces;

namespace LeatherShopAPI.Services;

public class BroadcastService : IBroadcastService
{
    private readonly AppDbContext _db;
    private readonly IWhatsAppService _whatsApp;
    private readonly ILogger<BroadcastService> _logger;
    private readonly IServiceScopeFactory _scopeFactory;

    public BroadcastService(AppDbContext db, IWhatsAppService whatsApp, ILogger<BroadcastService> logger, IServiceScopeFactory scopeFactory)
    {
        _db = db;
        _whatsApp = whatsApp;
        _logger = logger;
        _scopeFactory = scopeFactory;
    }

    public async Task<BroadcastResultDto> SendBroadcastAsync(BroadcastRequestDto dto)
    {
        List<string> recipients;

        if (dto.PhoneNumbers != null && dto.PhoneNumbers.Any())
        {
            recipients = dto.PhoneNumbers
                .Select(p => p.Trim().Replace(" ", "").Replace("-", ""))
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

        var broadcastId = broadcast.Id;

        // Send messages in background with its own DbContext scope
        _ = Task.Run(async () =>
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var whatsApp = scope.ServiceProvider.GetRequiredService<IWhatsAppService>();
            var record = await db.BroadcastMessages.FindAsync(broadcastId);
            if (record == null) return;

            foreach (var phone in recipients)
            {
                try
                {
                    await whatsApp.SendTemplateMessage(
                        phone,
                        dto.TemplateName,
                        dto.LanguageCode,
                        dto.Parameters,
                        dto.ImageUrl
                    );
                    record.SentCount++;
                    await Task.Delay(15);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to send broadcast to {Phone}", phone);
                    record.FailedCount++;
                }
            }

            await db.SaveChangesAsync();
            _logger.LogInformation("Broadcast {Id} completed. Sent: {Sent}, Failed: {Failed}",
                record.Id, record.SentCount, record.FailedCount);
        });

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
