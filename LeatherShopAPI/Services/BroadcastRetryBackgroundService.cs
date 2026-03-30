using System.Text.Json;
using System.Threading.Channels;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.SignalR;
using LeatherShopAPI.Data;
using LeatherShopAPI.DTOs.Broadcast;
using LeatherShopAPI.Models;
using LeatherShopAPI.Models.WhatsApp;
using LeatherShopAPI.Services.ChatBot;
using LeatherShopAPI.Services.Interfaces;
using LeatherShopAPI.Hubs;

namespace LeatherShopAPI.Services;

/// <summary>
/// Thread-safe channel for triggering immediate retry processing.
/// When the admin clicks "Retry Failed", a signal is written so the background
/// service wakes up immediately instead of waiting for the next 30-min poll.
/// </summary>
public sealed class BroadcastRetryChannel
{
    private readonly Channel<int> _channel =
        Channel.CreateBounded<int>(new BoundedChannelOptions(8)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true
        });

    public ChannelWriter<int> Writer => _channel.Writer;
    public ChannelReader<int> Reader => _channel.Reader;
}

/// <summary>
/// Background service that periodically retries broadcast recipients who failed
/// due to Meta's per-user marketing frequency cap (error 131049).
///
/// How it works:
///   - Runs every 30 minutes (configurable)
///   - Queries BroadcastRecipients where Status == Failed AND NextRetryAt &lt;= now AND RetryCount &lt; MaxRetries
///   - Loads the parent BroadcastMessage to reconstruct the original template/parameters
///   - Re-sends the template message via WhatsApp API
///   - On success: resets status to Sent, stores new wamid, clears NextRetryAt
///   - On 131049 again: increments RetryCount, schedules next retry with exponential backoff
///   - On other error or max retries: marks as permanently failed (clears NextRetryAt)
///
/// Why this is needed:
///   Error 131049 is Meta's dynamic per-user marketing message cap. It's TEMPORARY —
///   the same user who gets blocked at 8 PM may accept messages at 10 AM the next day.
///   Meta explicitly recommends waiting at least 24 hours before retrying.
///
/// Backoff schedule: 24h → 48h → 72h (max 3 retries).
/// </summary>
public sealed class BroadcastRetryBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IConfiguration _config;
    private readonly ILogger<BroadcastRetryBackgroundService> _logger;
    private readonly BroadcastRetryChannel _retryChannel;
    private readonly IHubContext<NotificationHub> _hub;

    /// <summary>How often to check for retryable recipients.</summary>
    private static readonly TimeSpan CheckInterval = TimeSpan.FromMinutes(30);

    /// <summary>Maximum retry attempts per recipient.</summary>
    private const int MaxRetries = 3;

    /// <summary>Max recipients to process per timer-based cycle.</summary>
    private const int TimerBatchSize = 50;

    public BroadcastRetryBackgroundService(
        IServiceScopeFactory scopeFactory,
        IConfiguration config,
        ILogger<BroadcastRetryBackgroundService> logger,
        BroadcastRetryChannel retryChannel,
        IHubContext<NotificationHub> hub)
    {
        _scopeFactory = scopeFactory;
        _config = config;
        _logger = logger;
        _retryChannel = retryChannel;
        _hub = hub;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("BroadcastRetryBackgroundService started (check interval: {Interval})", CheckInterval);

        // Wait a bit on startup to let DB migrations complete
        await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // Timer-based: process a small global batch (all broadcasts, limited to TimerBatchSize)
                await ProcessTimerBatchAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in BroadcastRetryBackgroundService timer retry cycle");
            }

            // Wait for either the 30-min interval OR an immediate trigger from admin
            try
            {
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
                cts.CancelAfter(CheckInterval);
                var broadcastId = await _retryChannel.Reader.ReadAsync(cts.Token);

                if (broadcastId > 0)
                {
                    _logger.LogInformation("BroadcastRetryService: admin triggered retry for broadcast {BroadcastId}", broadcastId);
                    await ProcessAdminRetryAsync(broadcastId, stoppingToken);
                }
            }
            catch (OperationCanceledException)
            {
                // Either stoppingToken fired (shutdown) or CancelAfter expired (normal 30-min poll)
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in BroadcastRetryBackgroundService admin retry");
            }

            // Drain any queued channel items (in case multiple retries were queued)
            while (_retryChannel.Reader.TryRead(out var queuedId))
            {
                if (queuedId > 0)
                {
                    try
                    {
                        await ProcessAdminRetryAsync(queuedId, stoppingToken);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error processing queued admin retry for broadcast {BroadcastId}", queuedId);
                    }
                }
            }
        }

        _logger.LogInformation("BroadcastRetryBackgroundService stopped");
    }

    /// <summary>
    /// Timer-based: processes a small batch of retryable recipients across all broadcasts.
    /// Runs every 30 minutes. No SignalR events (background cleanup).
    /// </summary>
    private async Task ProcessTimerBatchAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var now = DateTime.UtcNow;

        var retryableRecipients = await db.BroadcastRecipients
            .Where(r => r.Status == BroadcastDeliveryStatus.Failed
                        && r.NextRetryAt != null
                        && r.NextRetryAt <= now
                        && r.RetryCount < MaxRetries)
            .OrderBy(r => r.NextRetryAt)
            .Take(TimerBatchSize)
            .Include(r => r.BroadcastMessage)
            .ToListAsync(ct);

        if (retryableRecipients.Count == 0) return;

        _logger.LogInformation("BroadcastRetryService: timer batch found {Count} recipients", retryableRecipients.Count);

        await ProcessRecipientList(db, retryableRecipients, broadcastId: 0, emitSignalR: false, ct);
    }

    /// <summary>
    /// Admin-triggered: processes ALL retryable recipients for a specific broadcast.
    /// Emits SignalR progress events so the frontend shows a progress bar.
    /// </summary>
    private async Task ProcessAdminRetryAsync(int broadcastId, CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var now = DateTime.UtcNow;

        var retryableRecipients = await db.BroadcastRecipients
            .Where(r => r.BroadcastMessageId == broadcastId
                        && r.Status == BroadcastDeliveryStatus.Failed
                        && r.NextRetryAt != null
                        && r.NextRetryAt <= now
                        && r.RetryCount < MaxRetries)
            .OrderBy(r => r.NextRetryAt)
            .Include(r => r.BroadcastMessage)
            .ToListAsync(ct);

        if (retryableRecipients.Count == 0)
        {
            // Emit completed event even if nothing to process
            await EmitRetryProgress(broadcastId, 0, 0, 0, 0, "completed");
            return;
        }

        _logger.LogInformation("BroadcastRetryService: admin retry for broadcast {BroadcastId}, processing {Count} recipients",
            broadcastId, retryableRecipients.Count);

        await ProcessRecipientList(db, retryableRecipients, broadcastId, emitSignalR: true, ct);
    }

    /// <summary>
    /// Core processing: retries a list of recipients. Optionally emits SignalR progress events.
    /// </summary>
    private async Task ProcessRecipientList(
        AppDbContext db, List<BroadcastRecipient> recipients,
        int broadcastId, bool emitSignalR, CancellationToken ct)
    {
        int total = recipients.Count;
        int processed = 0, succeeded = 0, failed = 0;

        var grouped = recipients.GroupBy(r => r.BroadcastMessageId);

        foreach (var group in grouped)
        {
            var broadcast = group.First().BroadcastMessage;
            var parameters = !string.IsNullOrEmpty(broadcast.ParametersJson)
                ? JsonSerializer.Deserialize<List<string>>(broadcast.ParametersJson)
                : null;

            List<CarouselCard>? carouselCards = null;
            if (broadcast.IsCarousel && !string.IsNullOrEmpty(broadcast.CarouselCardsJson))
            {
                var cardDtos = JsonSerializer.Deserialize<List<CarouselCardDto>>(broadcast.CarouselCardsJson);
                carouselCards = cardDtos?.Select(c => new CarouselCard
                {
                    ImageUrl = ResolveImageUrl(c.ImageUrl) ?? c.ImageUrl,
                    BodyParam = c.BodyParam.Length > 160 ? c.BodyParam[..160] : c.BodyParam,
                    ButtonPayload = c.ButtonPayload
                }).ToList();
            }

            foreach (var recipient in group)
            {
                if (ct.IsCancellationRequested) return;

                try
                {
                    var ok = await RetryRecipientAsync(recipient, broadcast, parameters, carouselCards, ct);
                    if (ok) succeeded++; else failed++;
                }
                catch (Exception ex)
                {
                    failed++;
                    _logger.LogError(ex, "Unexpected error retrying recipient {RecipientId} (phone {Phone})",
                        recipient.Id, recipient.Phone);
                }

                processed++;

                // Save + emit progress every 10 recipients
                if (processed % 10 == 0)
                {
                    await db.SaveChangesAsync(ct);

                    if (emitSignalR)
                    {
                        await EmitRetryProgress(broadcastId, processed, succeeded, failed, total, "processing");
                    }
                }

                // Small delay to avoid Meta rate-limiting
                await Task.Delay(100, ct);
            }
        }

        await db.SaveChangesAsync(ct);

        if (emitSignalR)
        {
            await EmitRetryProgress(broadcastId, processed, succeeded, failed, total, "completed");
        }

        _logger.LogInformation(
            "BroadcastRetryService: completed retry for broadcast {BroadcastId}. " +
            "Processed={Processed}, Succeeded={Succeeded}, Failed={Failed}",
            broadcastId, processed, succeeded, failed);
    }

    private async Task EmitRetryProgress(int broadcastId, int processed, int succeeded, int failed, int total, string status)
    {
        await _hub.Clients.Group("admins").SendAsync("BroadcastRetryProgress", new
        {
            broadcastId,
            processed,
            succeeded,
            failed,
            total,
            status
        });
    }

    private async Task<bool> RetryRecipientAsync(
        BroadcastRecipient recipient,
        BroadcastMessage broadcast,
        List<string>? parameters,
        List<CarouselCard>? carouselCards,
        CancellationToken ct)
    {
        try
        {
            using var sendScope = _scopeFactory.CreateScope();
            var whatsApp = sendScope.ServiceProvider.GetRequiredService<IWhatsAppService>();

            string? wamId;
            if (broadcast.IsCarousel && carouselCards != null && carouselCards.Count > 0)
            {
                wamId = await whatsApp.SendCarouselTemplateMessage(
                    recipient.Phone, broadcast.MessageTemplate,
                    carouselCards, broadcast.LanguageCode);
            }
            else
            {
                wamId = await whatsApp.SendTemplateMessage(
                    recipient.Phone, broadcast.MessageTemplate, broadcast.LanguageCode,
                    parameters, ResolveImageUrl(broadcast.ImageUrl) ?? broadcast.ImageUrl);
            }

            // Success! Reset status to Sent so webhook status tracking resumes for the new wamid
            recipient.Status = BroadcastDeliveryStatus.Sent;
            recipient.WamId = wamId;
            recipient.SentAt = DateTime.UtcNow;
            recipient.FailedAt = null;
            recipient.ErrorDetail = null;
            recipient.NextRetryAt = null;
            recipient.RetryCount++;

            AppendRetryHistory(recipient, succeeded: true, error: null);

            _logger.LogInformation(
                "Retry #{RetryNum} succeeded for recipient {RecipientId} (phone {Phone}, broadcast {BroadcastId}). New wamid: {WamId}",
                recipient.RetryCount, recipient.Id, recipient.Phone, broadcast.Id, wamId);

            return true;
        }
        catch (Exception ex)
        {
            recipient.RetryCount++;
            recipient.FailedAt = DateTime.UtcNow;

            var errorMsg = ex.Message.Length > 1000 ? ex.Message[..1000] : ex.Message;
            recipient.ErrorDetail = $"Retry #{recipient.RetryCount} failed: {errorMsg}";

            AppendRetryHistory(recipient, succeeded: false, error: errorMsg);

            // Check if Meta returned 131049 again (embedded in exception message)
            var is131049 = ex.Message.Contains("131049");

            if (is131049 && recipient.RetryCount < MaxRetries)
            {
                // Schedule another retry with exponential backoff
                var backoffHours = 24 * (recipient.RetryCount + 1);
                recipient.NextRetryAt = DateTime.UtcNow.AddHours(backoffHours);

                _logger.LogInformation(
                    "Retry #{RetryNum} for recipient {RecipientId} (phone {Phone}) hit 131049 again. " +
                    "Scheduled retry #{NextRetry} at {NextRetryAt:u}",
                    recipient.RetryCount, recipient.Id, recipient.Phone,
                    recipient.RetryCount + 1, recipient.NextRetryAt);
            }
            else
            {
                // Permanent failure or max retries exhausted — stop retrying
                recipient.NextRetryAt = null;

                _logger.LogWarning(
                    "Retry #{RetryNum} for recipient {RecipientId} (phone {Phone}) failed permanently: {Error}",
                    recipient.RetryCount, recipient.Id, recipient.Phone, errorMsg);
            }

            return false;
        }
    }

    private string? ResolveImageUrl(string? relativePath) => ChatBotHelpers.ResolveImageUrl(relativePath, _config);

    private static void AppendRetryHistory(BroadcastRecipient recipient, bool succeeded, string? error)
    {
        var history = string.IsNullOrEmpty(recipient.RetryHistoryJson)
            ? new List<RetryAttemptEntry>()
            : JsonSerializer.Deserialize<List<RetryAttemptEntry>>(recipient.RetryHistoryJson) ?? [];

        history.Add(new RetryAttemptEntry
        {
            Attempt = recipient.RetryCount,
            Timestamp = DateTime.UtcNow,
            Succeeded = succeeded,
            Error = error
        });

        recipient.RetryHistoryJson = JsonSerializer.Serialize(history);
    }
}
