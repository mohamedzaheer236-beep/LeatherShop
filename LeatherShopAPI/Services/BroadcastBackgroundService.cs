using System.Collections.Concurrent;
using System.Text.Json;
using System.Threading.Channels;
using Microsoft.EntityFrameworkCore;
using LeatherShopAPI.Data;
using LeatherShopAPI.DTOs.Broadcast;
using LeatherShopAPI.Models;
using LeatherShopAPI.Models.WhatsApp;
using LeatherShopAPI.Services.Interfaces;
using LeatherShopAPI.Services.ChatBot;
using Microsoft.AspNetCore.SignalR;
using LeatherShopAPI.Hubs;

namespace LeatherShopAPI.Services;

/// <summary>
/// Thread-safe channel for triggering broadcast processing.
/// Carries only the BroadcastId - all job data lives in the DB (restart-safe).
/// Registered as a Singleton so the same channel is shared between
/// BroadcastService (producer) and BroadcastBackgroundService (consumer).
/// </summary>
public sealed class BroadcastChannel
{
    private readonly Channel<int> _channel =
        Channel.CreateUnbounded<int>(new UnboundedChannelOptions
        {
            SingleReader = true  // only the background service reads
        });

    public ChannelWriter<int> Writer => _channel.Writer;
    public ChannelReader<int> Reader => _channel.Reader;
}

/// <summary>
/// Long-running hosted service that processes broadcast jobs.
///
/// Architecture (DB-backed + Channel hybrid):
///   - All broadcast data (recipients, template, params) stored in DB at creation time
///   - Channel<int> carries just the BroadcastId as an immediate trigger
///   - On startup, polls DB for incomplete broadcasts (Pending/Processing) and resumes them
///   - Processed phones tracked in DB so restarts resume precisely (no duplicates)
///   - Uses .Chunk(BatchSize) + Task.WhenAll for controlled concurrency (10 parallel sends)
///   - Progress saved every 50 messages to DB
///
/// Why this survives Railway restarts:
///   - Recipients stored in PostgreSQL (RecipientsJson), not in memory
///   - Processed phones tracked in PostgreSQL (ProcessedPhonesJson)
///   - On container restart, ExecuteAsync runs → ResumeIncompleteBroadcastsAsync
///     finds Pending/Processing rows → re-enqueues → resumes from last checkpoint
/// </summary>
public sealed class BroadcastBackgroundService : BackgroundService
{
    private readonly BroadcastChannel _channel;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IConfiguration _config;
    private readonly ILogger<BroadcastBackgroundService> _logger;
    private readonly IHubContext<NotificationHub> _hub;

    /// <summary>Max concurrent WhatsApp API calls per batch.</summary>
    private const int BatchSize = 10;

    /// <summary>Delay between batches to stay under Meta's per-second throughput limit (~50 msgs/sec).</summary>
    private const int BatchDelayMs = 200;

    /// <summary>Save DB progress every N messages.</summary>
    private const int BatchSaveInterval = 50;

    /// <summary>After this many messages, pause for an extra WaveDelayMs to spread load and reduce Meta spam detection.</summary>
    private const int WaveSize = 100;

    /// <summary>Extra delay (ms) between waves to reduce Meta's per-user marketing frequency cap triggers.</summary>
    private const int WaveDelayMs = 2000;

    public BroadcastBackgroundService(
        BroadcastChannel channel,
        IServiceScopeFactory scopeFactory,
        IConfiguration config,
        ILogger<BroadcastBackgroundService> logger,
        IHubContext<NotificationHub> hub)
    {
        _channel = channel;
        _scopeFactory = scopeFactory;
        _config = config;
        _logger = logger;
        _hub = hub;
    }

    /// <summary>
    /// Resolve a relative image path (e.g., /uploads/abc.jpg) to a full public URL.
    /// Delegates to shared ChatBotHelpers.ResolveImageUrl for consistent base URL resolution.
    /// </summary>
    private string? ResolveImageUrl(string? relativePath) => ChatBotHelpers.ResolveImageUrl(relativePath, _config);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("BroadcastBackgroundService started");

        // Resume any incomplete broadcasts from DB (survive Railway restart)
        await ResumeIncompleteBroadcastsAsync(stoppingToken);

        await foreach (var broadcastId in _channel.Reader.ReadAllAsync(stoppingToken))
        {
            try
            {
                await ProcessBroadcastAsync(broadcastId, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                _logger.LogWarning("Broadcast {BroadcastId} cancelled due to app shutdown", broadcastId);
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Broadcast {BroadcastId} failed unexpectedly", broadcastId);
            }
        }

        _logger.LogInformation("BroadcastBackgroundService stopped");
    }

    /// <summary>
    /// On startup, find any Pending or Processing broadcasts in DB and re-enqueue them.
    /// This handles the case where Railway restarted mid-broadcast.
    /// </summary>
    private async Task ResumeIncompleteBroadcastsAsync(CancellationToken ct)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var incompleteIds = await db.BroadcastMessages
                .Where(b => b.Status == BroadcastStatus.Pending || b.Status == BroadcastStatus.Processing)
                .OrderBy(b => b.SentAt)
                .Select(b => b.Id)
                .ToListAsync(ct);

            foreach (var id in incompleteIds)
            {
                _logger.LogInformation("Resuming incomplete broadcast {BroadcastId} from DB", id);
                await _channel.Writer.WriteAsync(id, ct);
            }

            if (incompleteIds.Count > 0)
                _logger.LogInformation("Enqueued {Count} incomplete broadcast(s) for resume", incompleteIds.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to query DB for incomplete broadcasts on startup");
        }
    }

    /// <summary>
    /// Process a single broadcast: load data from DB, compute remaining recipients,
    /// send with concurrency control, save progress periodically.
    /// </summary>
    private async Task ProcessBroadcastAsync(int broadcastId, CancellationToken ct)
    {
        // ── 1. Load broadcast from DB ──
        BroadcastMessage broadcast;
        using (var scope = _scopeFactory.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            broadcast = await db.BroadcastMessages.FirstOrDefaultAsync(b => b.Id == broadcastId, ct)
                        ?? throw new InvalidOperationException($"Broadcast {broadcastId} not found");

            if (broadcast.Status == BroadcastStatus.Completed)
            {
                _logger.LogInformation("Broadcast {BroadcastId} already completed, skipping", broadcastId);
                return;
            }

            // Mark as Processing
            broadcast.Status = BroadcastStatus.Processing;
            await db.SaveChangesAsync(ct);
        }

        // ── 2. Compute remaining recipients ──
        var allRecipients = JsonSerializer.Deserialize<List<string>>(broadcast.RecipientsJson) ?? [];
        var alreadyProcessed = JsonSerializer.Deserialize<HashSet<string>>(broadcast.ProcessedPhonesJson ?? "[]") ?? [];
        var remaining = allRecipients.Where(p => !alreadyProcessed.Contains(p)).ToList();

        if (remaining.Count == 0)
        {
            _logger.LogInformation("Broadcast {BroadcastId}: no remaining recipients, marking completed", broadcastId);
            await MarkCompletedAsync(broadcastId, broadcast.SentCount, broadcast.FailedCount, alreadyProcessed);
            return;
        }

        var isResume = alreadyProcessed.Count > 0;
        _logger.LogInformation(
            "Broadcast {BroadcastId}: sending to {Remaining} recipients{ResumeInfo} (batch={BatchSize}, delay={DelayMs}ms)",
            broadcastId, remaining.Count,
            isResume ? $" (resumed, {alreadyProcessed.Count} already processed)" : "",
            BatchSize, BatchDelayMs);

        // ── 2b. Create BroadcastRecipient records for remaining phones (if not resuming) ──
        // On first run, bulk-insert all recipient records as Queued.
        // On resume, they already exist — skip creation for already-processed phones.
        if (!isResume)
        {
            using var recipientScope = _scopeFactory.CreateScope();
            var recipientDb = recipientScope.ServiceProvider.GetRequiredService<AppDbContext>();

            var recipientRecords = remaining.Select(phone => new BroadcastRecipient
            {
                BroadcastMessageId = broadcastId,
                Phone = phone,
                Status = BroadcastDeliveryStatus.Queued
            }).ToList();

            recipientDb.BroadcastRecipients.AddRange(recipientRecords);
            await recipientDb.SaveChangesAsync(ct);

            _logger.LogInformation("Broadcast {BroadcastId}: created {Count} recipient tracking records", broadcastId, recipientRecords.Count);
        }
        else
        {
            // On resume: create any missing recipient records (phones not yet in the table)
            using var recipientScope = _scopeFactory.CreateScope();
            var recipientDb = recipientScope.ServiceProvider.GetRequiredService<AppDbContext>();

            var existingPhones = await recipientDb.BroadcastRecipients
                .Where(r => r.BroadcastMessageId == broadcastId)
                .Select(r => r.Phone)
                .ToListAsync(ct);

            var missingPhones = remaining.Except(existingPhones).ToList();
            if (missingPhones.Count > 0)
            {
                var newRecords = missingPhones.Select(phone => new BroadcastRecipient
                {
                    BroadcastMessageId = broadcastId,
                    Phone = phone,
                    Status = BroadcastDeliveryStatus.Queued
                }).ToList();
                recipientDb.BroadcastRecipients.AddRange(newRecords);
                await recipientDb.SaveChangesAsync(ct);
            }
        }

        // ── 3. Process remaining recipients in throttled batches ──
        // Sends BatchSize messages concurrently, then pauses BatchDelayMs before next batch.
        // This keeps throughput at ~50 msgs/sec - well under Meta's per-second limit.
        int sent = broadcast.SentCount, failed = broadcast.FailedCount;
        int totalProcessed = 0;
        var processedPhones = new ConcurrentBag<string>(alreadyProcessed);

        var parameters = !string.IsNullOrEmpty(broadcast.ParametersJson)
            ? JsonSerializer.Deserialize<List<string>>(broadcast.ParametersJson)
            : null;

        // Safety truncation: Meta limits total template body to 1024 chars.
        // Truncate any single parameter to 900 chars to leave room for template text + other params.
        if (parameters != null)
        {
            for (var i = 0; i < parameters.Count; i++)
            {
                if (parameters[i].Length > 900)
                    parameters[i] = parameters[i][..900];
            }
        }

        // Deserialize carousel cards if this is a carousel broadcast
        List<CarouselCard>? carouselCards = null;
        if (broadcast.IsCarousel && !string.IsNullOrEmpty(broadcast.CarouselCardsJson))
        {
            var cardDtos = JsonSerializer.Deserialize<List<CarouselCardDto>>(broadcast.CarouselCardsJson);
            if (cardDtos != null)
            {
                carouselCards = cardDtos.Select(c => new CarouselCard
                {
                    ImageUrl = ResolveImageUrl(c.ImageUrl) ?? c.ImageUrl,
                    // Meta limits hydrated carousel card body to 160 chars total.
                    // UI enforces the smart limit; backend caps at 160 as an absolute safety net.
                    BodyParam = c.BodyParam.Length > 160 ? c.BodyParam[..160] : c.BodyParam,
                    ButtonPayload = c.ButtonPayload
                }).ToList();

                // Validate all cards have resolved image URLs
                if (carouselCards.Any(c => string.IsNullOrWhiteSpace(c.ImageUrl)))
                {
                    _logger.LogError("Broadcast {BroadcastId}: carousel card has empty image URL after resolution, aborting", broadcastId);
                    await MarkCompletedAsync(broadcastId, 0, broadcast.TotalRecipients, alreadyProcessed);
                    return;
                }
            }
            else
            {
                // Carousel JSON is present but couldn't be deserialized — abort to avoid sending wrong format
                _logger.LogError("Broadcast {BroadcastId}: failed to deserialize carousel cards JSON, aborting", broadcastId);
                await MarkCompletedAsync(broadcastId, 0, broadcast.TotalRecipients, alreadyProcessed);
                return;
            }
        }
        // Process in chunks of BatchSize
        // Track consecutive failures to detect TEMPLATE-LEVEL errors (e.g. 132005 text too long,
        // 132001 template not found) that fail identically for ALL recipients.
        // Per-user errors (131049 marketing cap, 131026 not on WhatsApp) are NOT counted
        // because they vary per recipient — the next user may succeed.
        int consecutiveTemplateFailures = 0;
        string? lastTemplateError = null;
        const int TemplateFailThreshold = 20; // abort after 20 consecutive template-level failures

        // Known per-user error codes (do NOT trigger early abort)
        string[] perUserErrors = ["131049", "131026", "131047", "131051"];

        var batches = remaining.Chunk(BatchSize);
        foreach (var batch in batches)
        {
            if (ct.IsCancellationRequested) break;

            // Check early-abort condition before processing next batch
            if (consecutiveTemplateFailures >= TemplateFailThreshold)
            {
                _logger.LogWarning(
                    "Broadcast {BroadcastId}: aborting after {Count} consecutive template-level failures. Error: {Error}",
                    broadcastId, consecutiveTemplateFailures, lastTemplateError);

                // Mark all remaining Queued recipients as Failed
                using var abortScope = _scopeFactory.CreateScope();
                var abortDb = abortScope.ServiceProvider.GetRequiredService<AppDbContext>();
                var abortError = $"Broadcast aborted (template error): {lastTemplateError?[..Math.Min(lastTemplateError?.Length ?? 0, 500)]}";

                var abortedCount = await abortDb.BroadcastRecipients
                    .Where(r => r.BroadcastMessageId == broadcastId && r.Status == BroadcastDeliveryStatus.Queued)
                    .ExecuteUpdateAsync(s => s
                        .SetProperty(r => r.Status, BroadcastDeliveryStatus.Failed)
                        .SetProperty(r => r.ErrorDetail, abortError)
                        .SetProperty(r => r.FailedAt, DateTime.UtcNow), ct);

                Interlocked.Add(ref failed, abortedCount);
                _logger.LogWarning("Broadcast {BroadcastId}: marked {Count} queued recipients as failed (template-level abort)", broadcastId, abortedCount);
                break;
            }

            var tasks = batch.Select(async phone =>
            {
                try
                {
                    using var taskScope = _scopeFactory.CreateScope();
                    var whatsApp = taskScope.ServiceProvider.GetRequiredService<IWhatsAppService>();
                    var taskDb = taskScope.ServiceProvider.GetRequiredService<AppDbContext>();

                    string? wamId;
                    if (broadcast.IsCarousel && carouselCards != null && carouselCards.Count > 0)
                    {
                        wamId = await whatsApp.SendCarouselTemplateMessage(
                            phone, broadcast.MessageTemplate,
                            carouselCards, broadcast.LanguageCode);
                    }
                    else
                    {
                        wamId = await whatsApp.SendTemplateMessage(
                            phone, broadcast.MessageTemplate, broadcast.LanguageCode,
                            parameters, ResolveImageUrl(broadcast.ImageUrl) ?? broadcast.ImageUrl);
                    }

                    // Update recipient record: Queued → Sent + store wamid
                    await taskDb.BroadcastRecipients
                        .Where(r => r.BroadcastMessageId == broadcastId && r.Phone == phone)
                        .ExecuteUpdateAsync(s => s
                            .SetProperty(r => r.Status, BroadcastDeliveryStatus.Sent)
                            .SetProperty(r => r.WamId, wamId)
                            .SetProperty(r => r.SentAt, DateTime.UtcNow)
                            .SetProperty(r => r.OriginalSentAt, DateTime.UtcNow), ct);

                    Interlocked.Increment(ref sent);
                    Interlocked.Exchange(ref consecutiveTemplateFailures, 0); // reset on success
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Broadcast {BroadcastId}: failed to send to {Phone}",
                        broadcastId, phone);

                    // Update recipient record: Queued → Failed + store error
                    try
                    {
                        using var errScope = _scopeFactory.CreateScope();
                        var errDb = errScope.ServiceProvider.GetRequiredService<AppDbContext>();
                        var errorDetail = ex.Message.Length > 1000 ? ex.Message[..1000] : ex.Message;

                        await errDb.BroadcastRecipients
                            .Where(r => r.BroadcastMessageId == broadcastId && r.Phone == phone)
                            .ExecuteUpdateAsync(s => s
                                .SetProperty(r => r.Status, BroadcastDeliveryStatus.Failed)
                                .SetProperty(r => r.ErrorDetail, errorDetail)
                                .SetProperty(r => r.FailedAt, DateTime.UtcNow), ct);
                    }
                    catch (Exception dbEx)
                    {
                        _logger.LogWarning(dbEx, "Broadcast {BroadcastId}: failed to update recipient status for {Phone}", broadcastId, phone);
                    }

                    Interlocked.Increment(ref failed);
                    // Only count template-level errors for early-abort (not per-user errors)
                    var isPerUserError = perUserErrors.Any(code => ex.Message.Contains(code));
                    if (!isPerUserError)
                    {
                        Interlocked.Increment(ref consecutiveTemplateFailures);
                        lastTemplateError = ex.Message;
                    }
                }
                finally
                {
                    processedPhones.Add(phone);
                }
            });

            await Task.WhenAll(tasks);

            // Save progress periodically
            totalProcessed += batch.Length;
            if (totalProcessed % BatchSaveInterval < BatchSize)
            {
                await SaveProgressAsync(broadcastId, Volatile.Read(ref sent), Volatile.Read(ref failed), processedPhones);
            }

            // Emit real-time progress to admin dashboard via SignalR
            await _hub.Clients.Group("admins").SendAsync("BroadcastProgress", new
            {
                broadcastId,
                sent = Volatile.Read(ref sent),
                failed = Volatile.Read(ref failed),
                total = broadcast.TotalRecipients,
                status = "processing"
            }, ct);

            // Wave throttle: extra pause every WaveSize messages to spread load
            if (totalProcessed % WaveSize == 0 && !ct.IsCancellationRequested)
            {
                _logger.LogInformation("Broadcast {BroadcastId}: wave pause after {Processed}/{Total}",
                    broadcastId, totalProcessed, remaining.Count);
                try { await Task.Delay(WaveDelayMs, ct); }
                catch (OperationCanceledException) { /* handled below */ }
            }

            // Throttle: pause between batches to avoid Meta per-second rate limit
            if (!ct.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(BatchDelayMs, ct).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    // Shutdown requested during delay - save progress and exit gracefully
                    await SaveProgressAsync(broadcastId, Volatile.Read(ref sent), Volatile.Read(ref failed), processedPhones);
                    _logger.LogWarning("Broadcast {BroadcastId} interrupted by shutdown during delay. Progress saved.", broadcastId);
                    return;
                }
            }
        }

        // ── 4. Final save ──
        // If we broke out of the loop due to cancellation, save progress (not completed)
        if (ct.IsCancellationRequested)
        {
            await SaveProgressAsync(broadcastId, sent, failed, processedPhones);
            _logger.LogWarning(
                "Broadcast {BroadcastId} interrupted by shutdown after batch loop. Progress saved. Will resume on restart.",
                broadcastId);
            return;
        }

        // Normal completion - mark done
        await MarkCompletedAsync(broadcastId, sent, failed, processedPhones);

        // Emit final SignalR event
        await _hub.Clients.Group("admins").SendAsync("BroadcastProgress", new
        {
            broadcastId,
            sent,
            failed,
            total = broadcast.TotalRecipients,
            status = "completed"
        }, CancellationToken.None);

        _logger.LogInformation(
            "Broadcast {BroadcastId} completed. Sent: {Sent}, Failed: {Failed}",
            broadcastId, sent, failed);
    }

    /// <summary>
    /// Saves broadcast progress (counts + processed phones) using a stateless SQL UPDATE.
    /// Best-effort: failures are logged and skipped - next save or final save catches up.
    /// </summary>
    private async Task SaveProgressAsync(int broadcastId, int sent, int failed, ConcurrentBag<string> processedPhones)
    {
        try
        {
            var phonesJson = JsonSerializer.Serialize(processedPhones.ToArray());

            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            await db.BroadcastMessages
                .Where(b => b.Id == broadcastId)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(b => b.SentCount, sent)
                    .SetProperty(b => b.FailedCount, failed)
                    .SetProperty(b => b.ProcessedPhonesJson, phonesJson));
        }
        catch (Exception ex)
        {
            // Progress save is best-effort; next periodic save or final save catches up
            _logger.LogWarning(ex, "Failed to save progress for broadcast {BroadcastId}", broadcastId);
        }
    }

    /// <summary>
    /// Marks a broadcast as Completed with final counts and processed phones.
    /// </summary>
    private async Task MarkCompletedAsync(int broadcastId, int sent, int failed, IEnumerable<string> processedPhones)
    {
        try
        {
            var phonesJson = JsonSerializer.Serialize(processedPhones.ToArray());

            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            await db.BroadcastMessages
                .Where(b => b.Id == broadcastId)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(b => b.SentCount, sent)
                    .SetProperty(b => b.FailedCount, failed)
                    .SetProperty(b => b.ProcessedPhonesJson, phonesJson)
                    .SetProperty(b => b.Status, BroadcastStatus.Completed));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to mark broadcast {BroadcastId} as completed", broadcastId);
        }
    }
}
