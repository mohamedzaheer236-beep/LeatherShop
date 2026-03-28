using System.Text.Json;
using System.Threading.Channels;
using Microsoft.EntityFrameworkCore;
using LeatherShopAPI.Data;
using LeatherShopAPI.DTOs.Broadcast;
using LeatherShopAPI.Models;
using LeatherShopAPI.Models.WhatsApp;
using LeatherShopAPI.Services.ChatBot;
using LeatherShopAPI.Services.Interfaces;

namespace LeatherShopAPI.Services;

/// <summary>
/// Thread-safe channel for triggering immediate retry processing.
/// When the admin clicks "Retry Failed", a signal is written so the background
/// service wakes up immediately instead of waiting for the next 30-min poll.
/// </summary>
public sealed class BroadcastRetryChannel
{
    private readonly Channel<bool> _channel =
        Channel.CreateBounded<bool>(new BoundedChannelOptions(1)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true
        });

    public ChannelWriter<bool> Writer => _channel.Writer;
    public ChannelReader<bool> Reader => _channel.Reader;
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

    /// <summary>How often to check for retryable recipients.</summary>
    private static readonly TimeSpan CheckInterval = TimeSpan.FromMinutes(30);

    /// <summary>Maximum retry attempts per recipient.</summary>
    private const int MaxRetries = 3;

    /// <summary>Max recipients to process per cycle (avoid long-running DB locks).</summary>
    private const int BatchSize = 50;

    public BroadcastRetryBackgroundService(
        IServiceScopeFactory scopeFactory,
        IConfiguration config,
        ILogger<BroadcastRetryBackgroundService> logger,
        BroadcastRetryChannel retryChannel)
    {
        _scopeFactory = scopeFactory;
        _config = config;
        _logger = logger;
        _retryChannel = retryChannel;
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
                await ProcessRetryBatchAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in BroadcastRetryBackgroundService retry cycle");
            }

            // Wait for either the 30-min interval OR an immediate trigger from admin
            try
            {
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
                cts.CancelAfter(CheckInterval);
                await _retryChannel.Reader.ReadAsync(cts.Token);
                _logger.LogInformation("BroadcastRetryService: triggered immediately by admin retry request");
            }
            catch (OperationCanceledException)
            {
                // Either stoppingToken fired (shutdown) or CancelAfter expired (normal 30-min poll)
            }
        }

        _logger.LogInformation("BroadcastRetryBackgroundService stopped");
    }

    private async Task ProcessRetryBatchAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var now = DateTime.UtcNow;

        // Find recipients due for retry: Failed + NextRetryAt has passed + under retry limit
        var retryableRecipients = await db.BroadcastRecipients
            .Where(r => r.Status == BroadcastDeliveryStatus.Failed
                        && r.NextRetryAt != null
                        && r.NextRetryAt <= now
                        && r.RetryCount < MaxRetries)
            .OrderBy(r => r.NextRetryAt)
            .Take(BatchSize)
            .Include(r => r.BroadcastMessage)
            .ToListAsync(ct);

        if (retryableRecipients.Count == 0) return;

        _logger.LogInformation("BroadcastRetryService: found {Count} recipients due for retry", retryableRecipients.Count);

        // Group by broadcast for efficient parameter reuse
        var grouped = retryableRecipients.GroupBy(r => r.BroadcastMessageId);

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
                    await RetryRecipientAsync(recipient, broadcast, parameters, carouselCards, ct);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Unexpected error retrying recipient {RecipientId} (phone {Phone})",
                        recipient.Id, recipient.Phone);
                }
            }
        }

        await db.SaveChangesAsync(ct);
        _logger.LogInformation("BroadcastRetryService: completed retry cycle");
    }

    private async Task RetryRecipientAsync(
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
        }
    }

    private string? ResolveImageUrl(string? relativePath)
    {
        if (string.IsNullOrEmpty(relativePath)) return null;
        if (relativePath.StartsWith("http")) return relativePath;

        var baseUrl = ChatBotHelpers.GetPublicBaseUrl(_config);
        return baseUrl != null ? $"{baseUrl}{relativePath}" : null;
    }

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
