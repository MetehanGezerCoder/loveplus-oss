using System.Collections.Concurrent;
using LovePlus.Application.Heartbeat;
using StackExchange.Redis;

namespace LovePlus.Infrastructure.Realtime;

/// <summary>
/// Cross-instance SignalR presence backed by Redis.
///
/// Each API process owns a short-lived instance lease. User sets contain
/// instance-scoped connection members. If a process dies without receiving
/// OnDisconnectedAsync, its lease expires and another process can discard the
/// stale members during the next presence read.
/// </summary>
public sealed class RedisRealtimePresenceTracker : IRealtimePresenceTracker
{
    public static readonly TimeSpan InstanceLeaseTtl = TimeSpan.FromSeconds(45);
    public static readonly TimeSpan InstanceLeaseRefreshInterval = TimeSpan.FromSeconds(15);
    private static readonly TimeSpan UserConnectionsTtl = TimeSpan.FromMinutes(2);

    private readonly IConnectionMultiplexer _redis;
    private readonly string _instanceId = Guid.NewGuid().ToString("N");
    private readonly ConcurrentDictionary<string, Guid> _localConnections = new();

    public RedisRealtimePresenceTracker(IConnectionMultiplexer redis)
    {
        _redis = redis;
    }

    public async Task ConnectedAsync(Guid userId, string connectionId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var localKey = LocalConnectionKey(userId, connectionId);
        _localConnections[localKey] = userId;

        try
        {
            var database = _redis.GetDatabase();
            await database.StringSetAsync(InstanceKey(_instanceId), "1", InstanceLeaseTtl);
            await database.SetAddAsync(UserKey(userId), Member(connectionId));
            await database.KeyExpireAsync(UserKey(userId), UserConnectionsTtl);
        }
        catch
        {
            _localConnections.TryRemove(localKey, out _);
            throw;
        }
    }

    public async Task DisconnectedAsync(Guid userId, string connectionId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _localConnections.TryRemove(LocalConnectionKey(userId, connectionId), out _);

        var database = _redis.GetDatabase();
        var key = UserKey(userId);
        await database.SetRemoveAsync(key, Member(connectionId));
        if (await database.SetLengthAsync(key) == 0)
        {
            await database.KeyDeleteAsync(key);
        }
    }

    public async Task<bool> IsOnlineAsync(Guid userId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var database = _redis.GetDatabase();
        var key = UserKey(userId);
        var members = await database.SetMembersAsync(key);
        if (members.Length == 0)
        {
            return false;
        }

        var instanceIds = members
            .Select(ParseInstanceId)
            .Where(x => x is not null)
            .Select(x => x!)
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        var leaseChecks = instanceIds.ToDictionary(
            instanceId => instanceId,
            instanceId => database.KeyExistsAsync(InstanceKey(instanceId)),
            StringComparer.Ordinal);
        await Task.WhenAll(leaseChecks.Values);

        var liveInstances = leaseChecks
            .Where(x => x.Value.Result)
            .Select(x => x.Key)
            .ToHashSet(StringComparer.Ordinal);
        var staleMembers = members
            .Where(member =>
            {
                var instanceId = ParseInstanceId(member);
                return instanceId is null || !liveInstances.Contains(instanceId);
            })
            .ToArray();

        if (staleMembers.Length > 0)
        {
            await database.SetRemoveAsync(key, staleMembers);
        }

        return members.Any(member =>
        {
            var instanceId = ParseInstanceId(member);
            return instanceId is not null && liveInstances.Contains(instanceId);
        });
    }

    /// <summary>
    /// Keeps this process discoverable while it still owns SignalR connections.
    /// The hosted lease refresher calls this periodically.
    /// </summary>
    public async Task RefreshInstanceLeaseAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (_localConnections.IsEmpty)
        {
            return;
        }

        var database = _redis.GetDatabase();
        await database.StringSetAsync(InstanceKey(_instanceId), "1", InstanceLeaseTtl);
        foreach (var userId in _localConnections.Values.Distinct())
        {
            await database.KeyExpireAsync(UserKey(userId), UserConnectionsTtl);
        }
    }

    private string Member(string connectionId) => $"{_instanceId}|{connectionId}";
    private static string LocalConnectionKey(Guid userId, string connectionId) => $"{userId:N}|{connectionId}";
    private static RedisKey UserKey(Guid userId) => $"loveplus:presence:user:{userId:N}";
    private static RedisKey InstanceKey(string instanceId) => $"loveplus:presence:instance:{instanceId}";

    private static string? ParseInstanceId(RedisValue member)
    {
        var value = member.ToString();
        var separator = value.IndexOf('|');
        return separator > 0 ? value[..separator] : null;
    }
}
