using LovePlus.Application.Heartbeat;
using StackExchange.Redis;

namespace LovePlus.Infrastructure.Realtime;

public sealed class RedisHeartbeatStore(IConnectionMultiplexer redis) : IEphemeralHeartbeatStore
{
    private const string ReserveScript = """
        if redis.call('EXISTS', KEYS[1]) == 1 then return 2 end
        local senderCount = redis.call('INCR', KEYS[2])
        if senderCount == 1 then redis.call('PEXPIRE', KEYS[2], ARGV[1]) end
        local pairCount = redis.call('INCR', KEYS[3])
        if pairCount == 1 then redis.call('PEXPIRE', KEYS[3], ARGV[1]) end
        if senderCount > tonumber(ARGV[2]) or pairCount > tonumber(ARGV[2]) then return 3 end
        redis.call('HSET', KEYS[1],
            'pair', ARGV[3],
            'sender', ARGV[4],
            'receiver', ARGV[5],
            'expires', ARGV[6])
        redis.call('PEXPIRE', KEYS[1], ARGV[7])
        return 1
        """;

    private const string AcknowledgeScript = """
        if redis.call('EXISTS', KEYS[1]) == 0 then return {0} end
        local receiver = redis.call('HGET', KEYS[1], 'receiver')
        if receiver ~= ARGV[1] then return {-1} end
        local sender = redis.call('HGET', KEYS[1], 'sender')
        if redis.call('HSETNX', KEYS[1], 'ack', ARGV[2]) == 0 then return {2, sender} end
        redis.call('HSET', KEYS[1], 'ackAt', ARGV[3])
        return {1, sender}
        """;

    public async Task<HeartbeatReservationState> TryReserveAsync(
        HeartbeatEnvelope envelope,
        HeartbeatPolicy policy,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var result = await redis.GetDatabase().ScriptEvaluateAsync(
            ReserveScript,
            [EventKey(envelope.EventId), UserRateKey(envelope.SenderUserId), PairRateKey(envelope.PairId)],
            [
                (long)policy.RateWindow.TotalMilliseconds,
                policy.MaxEventsPerWindow,
                envelope.PairId.ToString("N"),
                envelope.SenderUserId.ToString("N"),
                envelope.ReceiverUserId.ToString("N"),
                envelope.ExpiresAtUtc.ToUnixTimeMilliseconds(),
                (long)policy.EventTtl.TotalMilliseconds
            ]);
        return (long)result switch
        {
            1 => HeartbeatReservationState.Accepted,
            2 => HeartbeatReservationState.Duplicate,
            _ => HeartbeatReservationState.RateLimited
        };
    }

    public async Task<HeartbeatAcknowledgementStoreResult> TryAcknowledgeAsync(
        Guid eventId,
        Guid receiverUserId,
        HeartbeatAcknowledgementState state,
        DateTimeOffset acknowledgedAtUtc,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var raw = await redis.GetDatabase().ScriptEvaluateAsync(
            AcknowledgeScript,
            [EventKey(eventId)],
            [receiverUserId.ToString("N"), state.ToString(), acknowledgedAtUtc.ToUnixTimeMilliseconds()]);
        var values = (RedisResult[])raw!;
        var code = (long)values[0];
        if (code == 0) return new(HeartbeatAcknowledgementResultState.Expired);
        if (code == -1) return new(HeartbeatAcknowledgementResultState.Forbidden);
        var sender = Guid.Parse((string)values[1]!);
        return code == 2
            ? new(HeartbeatAcknowledgementResultState.Duplicate, sender)
            : new(HeartbeatAcknowledgementResultState.Accepted, sender);
    }

    private static RedisKey EventKey(Guid eventId) => $"loveplus:heartbeat:event:{eventId:N}";
    private static RedisKey UserRateKey(Guid userId) => $"loveplus:heartbeat:rate:user:{userId:N}";
    private static RedisKey PairRateKey(Guid pairId) => $"loveplus:heartbeat:rate:pair:{pairId:N}";
}
