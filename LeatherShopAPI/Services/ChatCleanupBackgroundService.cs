using Microsoft.EntityFrameworkCore;
using LeatherShopAPI.Data;

namespace LeatherShopAPI.Services;

/// <summary>
/// Background service that runs daily and deletes ChatMessages older than 30 days.
/// This keeps the database lean without manual intervention.
/// </summary>
public class ChatCleanupBackgroundService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<ChatCleanupBackgroundService> _logger;
    private static readonly TimeSpan Interval = TimeSpan.FromHours(24);
    private static readonly TimeSpan MessageRetention = TimeSpan.FromDays(30);

    public ChatCleanupBackgroundService(IServiceProvider serviceProvider, ILogger<ChatCleanupBackgroundService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("ChatCleanupBackgroundService started. Runs every {Hours}h, deletes messages older than {Days} days.",
            Interval.TotalHours, MessageRetention.TotalDays);

        // Delay startup to avoid competing with migrations and initial requests on cold start
        await Task.Delay(TimeSpan.FromMinutes(2), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CleanupOldMessages(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during chat message cleanup");
            }

            await Task.Delay(Interval, stoppingToken);
        }
    }

    private async Task CleanupOldMessages(CancellationToken ct)
    {
        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var cutoff = DateTime.UtcNow - MessageRetention;

        var deletedCount = await db.ChatMessages
            .Where(m => m.Timestamp < cutoff)
            .ExecuteDeleteAsync(ct);

        if (deletedCount > 0)
        {
            _logger.LogInformation("ChatCleanup: Deleted {Count} messages older than {Date:yyyy-MM-dd}",
                deletedCount, cutoff);
        }
    }
}
