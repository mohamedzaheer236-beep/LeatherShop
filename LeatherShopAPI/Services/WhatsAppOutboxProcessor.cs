using Microsoft.EntityFrameworkCore;
using LeatherShopAPI.Data;
using LeatherShopAPI.Models;
using LeatherShopAPI.Services.Interfaces;

namespace LeatherShopAPI.Services;

/// <summary>
/// Transactional Outbox Processor — polls the WhatsAppOutboxMessages table for pending
/// messages and delivers them via WhatsApp Cloud API with exponential backoff.
///
/// Why outbox instead of in-memory Channel?
/// - Survives app restarts, container redeployments, and crashes (Railway rebuilds container on every push)
/// - Message is committed to DB in the same transaction as the order — atomic
/// - Full audit trail: admins can see pending/sent/failed messages
/// - No message loss, ever
///
/// Flow:
/// 1. ChatBotService.PlaceOrder() adds a WhatsAppOutboxMessage to DbContext in the same SaveChangesAsync()
/// 2. This processor wakes up every 10 seconds, queries for due Pending messages
/// 3. Attempts to send via IWhatsAppService (which already has transport-level retry for rate limits)
/// 4. On success: marks Sent. On failure: increments RetryCount, sets NextRetryAt with exponential backoff
/// 5. After MaxRetries: marks Failed — admin must manually follow up via the chat panel
///
/// Uses IServiceScopeFactory to create a fresh scope (DbContext + WhatsAppService) per polling cycle.
/// Same pattern as BroadcastBackgroundService.
/// </summary>
public sealed class WhatsAppOutboxProcessor : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<WhatsAppOutboxProcessor> _logger;

    /// <summary>How often to poll the outbox table for due messages (seconds)</summary>
    private const int PollIntervalSeconds = 10;

    /// <summary>
    /// Exponential backoff delays (seconds) indexed by RetryCount.
    /// Retry 0 = 30s, 1 = 60s, 2 = 120s, 3 = 300s, 4 = 600s.
    /// After retry 4 (5th attempt), the message is marked Failed.
    /// </summary>
    private static readonly int[] BackoffDelaysSeconds = [30, 60, 120, 300, 600];

    /// <summary>Max messages to process per polling cycle (prevents hogging the DB connection)</summary>
    private const int BatchSize = 10;

    public WhatsAppOutboxProcessor(
        IServiceScopeFactory scopeFactory,
        ILogger<WhatsAppOutboxProcessor> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("WhatsAppOutboxProcessor started — polling every {Interval}s", PollIntervalSeconds);

        // Small initial delay to let the app fully start
        await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessPendingMessagesAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "WhatsAppOutboxProcessor encountered an error during polling cycle");
            }

            await Task.Delay(TimeSpan.FromSeconds(PollIntervalSeconds), stoppingToken);
        }

        _logger.LogInformation("WhatsAppOutboxProcessor stopped");
    }

    /// <summary>
    /// Fetches pending messages that are due for (re)try and processes them one at a time.
    /// </summary>
    private async Task ProcessPendingMessagesAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var whatsApp = scope.ServiceProvider.GetRequiredService<IWhatsAppService>();

        var now = DateTime.UtcNow;

        // Fetch messages that are: Pending AND (never tried OR due for retry)
        var dueMessages = await db.WhatsAppOutboxMessages
            .Where(m => m.Status == OutboxMessageStatus.Pending
                        && (m.NextRetryAt == null || m.NextRetryAt <= now))
            .OrderBy(m => m.CreatedAt) // FIFO — oldest first
            .Take(BatchSize)
            .ToListAsync(ct);

        if (dueMessages.Count == 0)
            return;

        _logger.LogInformation("WhatsAppOutboxProcessor found {Count} due message(s)", dueMessages.Count);

        foreach (var message in dueMessages)
        {
            if (ct.IsCancellationRequested) break;
            await ProcessSingleMessageAsync(db, whatsApp, message);
        }
    }

    /// <summary>
    /// Attempts to send a single outbox message. Updates status in DB regardless of outcome.
    /// </summary>
    private async Task ProcessSingleMessageAsync(AppDbContext db, IWhatsAppService whatsApp, WhatsAppOutboxMessage message)
    {
        try
        {
            _logger.LogInformation(
                "Outbox: sending message {Id} (attempt {Attempt}/{Max}) — {Context}",
                message.Id, message.RetryCount + 1, message.MaxRetries, message.Context);

            await whatsApp.SendTextMessage(message.To, message.Content);

            // Success — mark as sent
            message.Status = OutboxMessageStatus.Sent;
            message.SentAt = DateTime.UtcNow;
            await db.SaveChangesAsync();

            _logger.LogInformation("Outbox: message {Id} sent successfully — {Context}", message.Id, message.Context);
        }
        catch (WhatsAppApiException ex)
        {
            message.RetryCount++;
            message.LastError = ex.Message.Length > 2000 ? ex.Message[..2000] : ex.Message;

            if (message.RetryCount >= message.MaxRetries)
            {
                // Exhausted all retries — mark as failed
                message.Status = OutboxMessageStatus.Failed;
                await db.SaveChangesAsync();

                _logger.LogError(ex,
                    "Outbox: message {Id} FAILED permanently after {Retries} attempts — {Context}. " +
                    "Admin must manually follow up via chat panel.",
                    message.Id, message.RetryCount, message.Context);
            }
            else
            {
                // Schedule next retry with exponential backoff
                var backoffIndex = Math.Min(message.RetryCount - 1, BackoffDelaysSeconds.Length - 1);
                var delaySec = BackoffDelaysSeconds[backoffIndex];
                message.NextRetryAt = DateTime.UtcNow.AddSeconds(delaySec);
                await db.SaveChangesAsync();

                _logger.LogWarning(ex,
                    "Outbox: message {Id} failed (attempt {Attempt}/{Max}), next retry in {Delay}s — {Context}",
                    message.Id, message.RetryCount, message.MaxRetries, delaySec, message.Context);
            }
        }
        catch (Exception ex)
        {
            // Unexpected error (network, DB, etc.) — treat like a WhatsApp failure
            message.RetryCount++;
            message.LastError = ex.Message.Length > 2000 ? ex.Message[..2000] : ex.Message;

            if (message.RetryCount >= message.MaxRetries)
            {
                message.Status = OutboxMessageStatus.Failed;
                await db.SaveChangesAsync();
                _logger.LogError(ex, "Outbox: message {Id} FAILED permanently (unexpected error) — {Context}", message.Id, message.Context);
            }
            else
            {
                var backoffIndex = Math.Min(message.RetryCount - 1, BackoffDelaysSeconds.Length - 1);
                var delaySec = BackoffDelaysSeconds[backoffIndex];
                message.NextRetryAt = DateTime.UtcNow.AddSeconds(delaySec);
                await db.SaveChangesAsync();
                _logger.LogWarning(ex, "Outbox: message {Id} failed unexpectedly (attempt {Attempt}/{Max}), retry in {Delay}s — {Context}",
                    message.Id, message.RetryCount, message.MaxRetries, delaySec, message.Context);
            }
        }
    }
}
