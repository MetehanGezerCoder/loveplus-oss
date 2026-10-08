using LovePlus.Infrastructure.Realtime;
using StackExchange.Redis;

namespace LovePlus.Api.IntegrationTests;

public sealed class RedisPresenceTrackerTests
{
    [Fact]
    public async Task Presence_is_shared_across_api_instances()
    {
        var connectionString = Environment.GetEnvironmentVariable("LOVEPLUS_TEST_REDIS");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return;
        }

        using var firstRedis = await ConnectionMultiplexer.ConnectAsync(connectionString);
        using var secondRedis = await ConnectionMultiplexer.ConnectAsync(connectionString);
        var firstInstance = new RedisRealtimePresenceTracker(firstRedis);
        var secondInstance = new RedisRealtimePresenceTracker(secondRedis);
        var userId = Guid.NewGuid();

        await firstInstance.ConnectedAsync(userId, "phone", default);
        Assert.True(await secondInstance.IsOnlineAsync(userId, default));

        await secondInstance.ConnectedAsync(userId, "tablet", default);
        await firstInstance.DisconnectedAsync(userId, "phone", default);
        Assert.True(await firstInstance.IsOnlineAsync(userId, default));

        await secondInstance.DisconnectedAsync(userId, "tablet", default);
        Assert.False(await firstInstance.IsOnlineAsync(userId, default));
    }
}
