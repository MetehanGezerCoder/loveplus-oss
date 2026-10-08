using LovePlus.Infrastructure.Realtime;
using StackExchange.Redis;

namespace LovePlus.Api.Startup;

public sealed class RedisPresenceLeaseService(
    RedisRealtimePresenceTracker presence,
    ILogger<RedisPresenceLeaseService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(RedisRealtimePresenceTracker.InstanceLeaseRefreshInterval);
        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                try
                {
                    await presence.RefreshInstanceLeaseAsync(stoppingToken);
                }
                catch (RedisException exception)
                {
                    logger.LogWarning(exception, "Could not refresh the Redis realtime-presence lease");
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Normal host shutdown.
        }
    }
}
