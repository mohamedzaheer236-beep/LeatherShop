using System.Threading.Channels;
using Microsoft.EntityFrameworkCore;
using LeatherShopAPI.Data;
using LeatherShopAPI.Services.Interfaces;

namespace LeatherShopAPI.Services;

/// <summary>
/// A job that represents a single broadcast to be processed in the background.
/// Immutable — all values captured at enqueue time, no closure over request-scoped objects.
/// </summary>
public sealed record BroadcastJob(
    int BroadcastId,
    List<string> Recipients,
    string TemplateName,
    string LanguageCode,
    List<string>? Parameters,
    string? ImageUrl
);

/// <summary>
/// Thread-safe channel for enqueuing broadcast jobs from any request thread.
/// Registered as a Singleton so the same channel is shared between
/// BroadcastService (producer) and BroadcastBackgroundService (consumer).
/// </summary>
public sealed class BroadcastChannel
{
    private readonly Channel<BroadcastJob> _channel =
        Channel.CreateUnbounded<BroadcastJob>(new UnboundedChannelOptions
        {
            SingleReader = true  // only the background service reads
        });

    public ChannelWriter<BroadcastJob> Writer => _channel.Writer;
    public ChannelReader<BroadcastJob> Reader => _channel.Reader;
}

/// <summary>
/// Long-running hosted service that processes broadcast jobs from the channel.
///
/// Why this is better than Task.Run:
///   - Managed by the .NET host: starts with the app, stops gracefully on shutdown
///   - Supports cancellation via CancellationToken (SIGTERM, app restart)
///   - Uses SemaphoreSlim for controlled concurrency instead of sequential Task.Delay
///   - Proper DI scoping per job (no closure over disposed request objects)
///   - Progress saved periodically and on completion
/// </summary>
public sealed class BroadcastBackgroundService : BackgroundService
{
    private readonly BroadcastChannel _channel;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<BroadcastBackgroundService> _logger;

    /// <summary>Max concurrent WhatsApp API calls per broadcast.</summary>
    private const int MaxConcurrency = 10;

    /// <summary>Save DB progress every N messages.</summary>
    private const int BatchSaveInterval = 50;

    public BroadcastBackgroundService(
        BroadcastChannel channel,
        IServiceScopeFactory scopeFactory,
        ILogger<BroadcastBackgroundService> logger)
    {
        _channel = channel;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("BroadcastBackgroundService started");

        await foreach (var job in _channel.Reader.ReadAllAsync(stoppingToken))
        {
            try
            {
                await ProcessBroadcastAsync(job, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                _logger.LogWarning("Broadcast {BroadcastId} cancelled due to app shutdown", job.BroadcastId);
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Broadcast {BroadcastId} failed unexpectedly", job.BroadcastId);
            }
        }

        _logger.LogInformation("BroadcastBackgroundService stopped");
    }

    private async Task ProcessBroadcastAsync(BroadcastJob job, CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var whatsApp = scope.ServiceProvider.GetRequiredService<IWhatsAppService>();

        var record = await db.BroadcastMessages.FindAsync(new object[] { job.BroadcastId }, ct);
        if (record == null)
        {
            _logger.LogWarning("Broadcast {BroadcastId} record not found, skipping", job.BroadcastId);
            return;
        }

        _logger.LogInformation(
            "Broadcast {BroadcastId}: sending to {Count} recipients (concurrency={Concurrency})",
            job.BroadcastId, job.Recipients.Count, MaxConcurrency);

        int sent = 0, failed = 0;
        var semaphore = new SemaphoreSlim(MaxConcurrency);

        // Process all recipients with controlled concurrency
        var tasks = job.Recipients.Select(async phone =>
        {
            await semaphore.WaitAsync(ct);
            try
            {
                await whatsApp.SendTemplateMessage(
                    phone, job.TemplateName, job.LanguageCode, job.Parameters, job.ImageUrl);
                Interlocked.Increment(ref sent);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Broadcast {BroadcastId}: failed to send to {Phone}",
                    job.BroadcastId, phone);
                Interlocked.Increment(ref failed);
            }
            finally
            {
                semaphore.Release();
            }

            // Save progress periodically (thread-safe check)
            var processed = Volatile.Read(ref sent) + Volatile.Read(ref failed);
            if (processed % BatchSaveInterval == 0)
            {
                await SaveProgressAsync(db, record, sent, failed);
            }
        });

        await Task.WhenAll(tasks);

        // Final save
        record.SentCount = sent;
        record.FailedCount = failed;
        await db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Broadcast {BroadcastId} completed. Sent: {Sent}, Failed: {Failed}",
            job.BroadcastId, sent, failed);
    }

    private static async Task SaveProgressAsync(AppDbContext db, Models.BroadcastMessage record, int sent, int failed)
    {
        try
        {
            record.SentCount = sent;
            record.FailedCount = failed;
            await db.SaveChangesAsync();
        }
        catch
        {
            // Progress save is best-effort; final save will catch up
        }
    }
}
