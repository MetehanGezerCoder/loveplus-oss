using System.Text.Json;
using System.Text.Json.Serialization;
using LovePlus.Application.Realtime;
using LovePlus.Domain.Realtime;
using StackExchange.Redis;

namespace LovePlus.Infrastructure.Realtime;

public sealed class RedisLiveStatusStore(IConnectionMultiplexer redis) : ILiveStatusStore
{
    private const string MoodScript = """
        local current = redis.call('GET', KEYS[1])
        if not current then return false end
        local decoded = cjson.decode(current)
        decoded.Mood = ARGV[1]
        decoded.IsMoodSharingEnabled = ARGV[2] == '1'
        local updated = cjson.encode(decoded)
        redis.call('SET', KEYS[1], updated, 'PX', ARGV[3])
        return updated
        """;
    private const string SequenceScript = """
        local current = redis.call('GET', KEYS[1])
        if current then
            local decoded = cjson.decode(current)
            if decoded.DeviceId == ARGV[1] then
                if tonumber(decoded.SequenceNumber) >= tonumber(ARGV[2]) then
                    return 0
                end
            elseif tonumber(decoded.RecordedAtUnixMilliseconds or 0) >= tonumber(ARGV[3]) then
                return 0
            end
        end
        redis.call('SET', KEYS[1], ARGV[4], 'PX', ARGV[5])
        return 1
        """;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = null,
        Converters = { new JsonStringEnumConverter() }
    };

    public async Task<UserLiveStatus?> GetAsync(Guid userId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var value = await redis.GetDatabase().StringGetAsync(Key(userId));
        return value.IsNullOrEmpty
            ? null
            : JsonSerializer.Deserialize<UserLiveStatus>((string)value!, JsonOptions);
    }

    public async Task<bool> TrySetAsync(
        UserLiveStatus status,
        TimeSpan ttl,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var result = await redis.GetDatabase().ScriptEvaluateAsync(
            SequenceScript,
            [Key(status.UserId)],
            [
                status.DeviceId,
                status.SequenceNumber,
                status.RecordedAtUnixMilliseconds,
                JsonSerializer.Serialize(status, JsonOptions),
                (long)ttl.TotalMilliseconds
            ]);
        return (long)result == 1;
    }

    public async Task<UserLiveStatus?> UpdateMoodAsync(
        Guid userId,
        MoodType mood,
        bool isMoodSharingEnabled,
        TimeSpan ttl,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var result = await redis.GetDatabase().ScriptEvaluateAsync(
            MoodScript,
            [Key(userId)],
            [mood.ToString(), isMoodSharingEnabled ? "1" : "0", (long)ttl.TotalMilliseconds]);
        return result.IsNull
            ? null
            : JsonSerializer.Deserialize<UserLiveStatus>((string)result!, JsonOptions);
    }

    private static RedisKey Key(Guid userId) => $"loveplus:status:{userId:N}";
}
