namespace Planora.Api.Infrastructure.Jobs;

/// <summary>
/// Periodically runs <see cref="DataCleanupRunner"/> in its own DI scope. The first pass fires
/// one interval after startup (not at boot), so a short-lived process — including the test host —
/// never triggers a purge. Interval and trash retention are configurable; failures are logged and
/// the loop keeps running.
/// </summary>
public sealed class DataCleanupBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<DataCleanupBackgroundService> _logger;
    private readonly TimeSpan _interval;
    private readonly TimeSpan _trashRetention;

    public DataCleanupBackgroundService(
        IServiceScopeFactory scopeFactory,
        IConfiguration config,
        ILogger<DataCleanupBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _interval = TimeSpan.FromHours(config.GetValue("Cleanup:IntervalHours", 6d));
        _trashRetention = TimeSpan.FromDays(config.GetValue("Cleanup:TrashRetentionDays", 30d));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(_interval);
        try
        {
            // WaitForNextTickAsync first => no run at startup; first pass is one interval later.
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                try
                {
                    await using var scope = _scopeFactory.CreateAsyncScope();
                    var runner = scope.ServiceProvider.GetRequiredService<DataCleanupRunner>();
                    await runner.RunAsync(DateTime.UtcNow, _trashRetention, stoppingToken);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    _logger.LogError(ex, "Scheduled data cleanup failed; will retry next interval.");
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Host is shutting down — expected.
        }
    }
}
