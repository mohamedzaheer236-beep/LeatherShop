using System.Collections.Concurrent;
using System.Text.Json;
using System.Threading.Channels;
using Microsoft.EntityFrameworkCore;
using LeatherShopAPI.Data;
using LeatherShopAPI.DTOs.Broadcast;
using LeatherShopAPI.Models;
using LeatherShopAPI.Services.Interfaces;

namespace LeatherShopAPI.Services;

/// <summary>
/// Thread-safe channel for triggering broadcast processing.
/// Carries only the BroadcastId — all job data lives in the DB (restart-safe).
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

    /// <summary>Max concurrent WhatsApp API calls per batch.</summary>
    private const int BatchSize = 10;

    /// <summary>Delay between batches to stay under Meta's per-second throughput limit (~50 msgs/sec).</summary>
    private const int BatchDelayMs = 200;

    /// <summary>Save DB progress every N messages.</summary>
    private const int BatchSaveInterval = 50;

    public BroadcastBackgroundService(
        BroadcastChannel channel,
        IServiceScopeFactory scopeFactory,
        IConfiguration config,
        ILogger<BroadcastBackgroundService> logger)
    {
        _channel = channel;
        _scopeFactory = scopeFactory;
        _config = config;
        _logger = logger;
    }

    /// <summary>
    /// Resolve a relative image path (e.g., /uploads/abc.jpg) to a full public URL.
    /// Uses App:BaseUrl config with RAILWAY_PUBLIC_DOMAIN fallback.
    /// </summary>
    private string? ResolveImageUrl(string? relativePath)
    {
        if (string.IsNullOrEmpty(relativePath)) return null;
        if (relativePath.StartsWith("http")) return relativePath; // already full URL

        var baseUrl = _config["App:BaseUrl"]?.TrimEnd('/');
        if (string.IsNullOrEmpty(baseUrl) || baseUrl.Contains("WILL_BE_SET") || baseUrl.Contains("localhost"))
        {
            var railwayDomain = Environment.GetEnvironmentVariable("RAILWAY_PUBLIC_DOMAIN");
            if (!string.IsNullOrEmpty(railwayDomain))
                baseUrl = $"https://{railwayDomain}";
            else
                return null;
        }
        return $"{baseUrl}{relativePath}";
    }

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

        // ── 3. Process remaining recipients in throttled batches ──
        // Sends BatchSize messages concurrently, then pauses BatchDelayMs before next batch.
        // This keeps throughput at ~50 msgs/sec — well under Meta's per-second limit.
        int sent = broadcast.SentCount, failed = broadcast.FailedCount;
        int totalProcessed = 0;
        var processedPhones = new ConcurrentBag<string>(alreadyProcessed);

        var parameters = !string.IsNullOrEmpty(broadcast.ParametersJson)
            ? JsonSerializer.Deserialize<List<string>>(broadcast.ParametersJson)
            : null;

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
                    // Meta limits hydrated carousel card body to 160 chars total
                    // (template text + parameter). Cap param at 120 to leave room.
                    BodyParam = c.BodyParam.Length > 120 ? c.BodyParam[..120] : c.BodyParam,
                    ButtonPayload = c.ButtonPayload
                }).ToList();
            }
        }
        // Process in chunks of BatchSize
        var batches = remaining.Chunk(BatchSize);
        foreach (var batch in batches)
        {
            if (ct.IsCancellationRequested) break;

            var tasks = batch.Select(async phone =>
            {
                try
                {
                    using var taskScope = _scopeFactory.CreateScope();
                    var whatsApp = taskScope.ServiceProvider.GetRequiredService<IWhatsAppService>();

                    if (broadcast.IsCarousel && carouselCards != null && carouselCards.Count > 0)
                    {
                        await whatsApp.SendCarouselTemplateMessage(
                            phone, broadcast.MessageTemplate,
                            carouselCards, broadcast.LanguageCode);
                    }
                    else
                    {
                        await whatsApp.SendTemplateMessage(
                            phone, broadcast.MessageTemplate, broadcast.LanguageCode,
                            parameters, ResolveImageUrl(broadcast.ImageUrl) ?? broadcast.ImageUrl);
                    }

                    Interlocked.Increment(ref sent);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Broadcast {BroadcastId}: failed to send to {Phone}",
                        broadcastId, phone);
                    Interlocked.Increment(ref failed);
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

            // Throttle: pause between batches to avoid Meta per-second rate limit
            if (!ct.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(BatchDelayMs, ct).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    // Shutdown requested during delay — save progress and exit gracefully
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

        // Normal completion — mark done
        await MarkCompletedAsync(broadcastId, sent, failed, processedPhones);

        _logger.LogInformation(
            "Broadcast {BroadcastId} completed. Sent: {Sent}, Failed: {Failed}",
            broadcastId, sent, failed);
    }

    /// <summary>
    /// Saves broadcast progress (counts + processed phones) using a stateless SQL UPDATE.
    /// Best-effort: failures are logged and skipped — next save or final save catches up.
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
