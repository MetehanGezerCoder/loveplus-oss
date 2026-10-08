using System.Collections.Concurrent;
using LovePlus.Application.Realtime;
using LovePlus.Domain.Realtime;

namespace LovePlus.Infrastructure.Realtime;

/// <summary>
/// Development-only status store used by the USB demo host when Redis is unavailable.
/// It preserves the same per-user sequence and TTL semantics without generating telemetry.
/// </summary>
public sealed class InMemoryLiveStatusStore : ILiveStatusStore
{
    private readonly ConcurrentDictionary<Guid, Entry> _entries = new();

    public Task<UserLiveStatus?> GetAsync(Guid userId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!_entries.TryGetValue(userId, out var entry))
        {
            return Task.FromResult<UserLiveStatus?>(null);
        }

        if (entry.ExpiresAtUtc > DateTimeOffset.UtcNow)
        {
            return Task.FromResult<UserLiveStatus?>(entry.Status);
        }

        _entries.TryRemove(new KeyValuePair<Guid, Entry>(userId, entry));
        return Task.FromResult<UserLiveStatus?>(null);
    }

    public Task<bool> TrySetAsync(
        UserLiveStatus status,
        TimeSpan ttl,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var expiresAtUtc = DateTimeOffset.UtcNow.Add(ttl);

        while (true)
        {
            if (!_entries.TryGetValue(status.UserId, out var current))
            {
                if (_entries.TryAdd(status.UserId, new Entry(status, expiresAtUtc)))
                {
                    return Task.FromResult(true);
                }
                continue;
            }

            if (current.ExpiresAtUtc > DateTimeOffset.UtcNow
                && ((current.Status.DeviceId == status.DeviceId
                        && current.Status.SequenceNumber >= status.SequenceNumber)
                    || (current.Status.DeviceId != status.DeviceId
                        && current.Status.RecordedAtUtc >= status.RecordedAtUtc)))
            {
                return Task.FromResult(false);
            }

            if (_entries.TryUpdate(status.UserId, new Entry(status, expiresAtUtc), current))
            {
                return Task.FromResult(true);
            }
        }
    }

    public Task<UserLiveStatus?> UpdateMoodAsync(
        Guid userId,
        MoodType mood,
        bool isMoodSharingEnabled,
        TimeSpan ttl,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        while (_entries.TryGetValue(userId, out var current))
        {
            if (current.ExpiresAtUtc <= DateTimeOffset.UtcNow)
            {
                _entries.TryRemove(new KeyValuePair<Guid, Entry>(userId, current));
                return Task.FromResult<UserLiveStatus?>(null);
            }
            var updated = current.Status with { Mood = mood, IsMoodSharingEnabled = isMoodSharingEnabled };
            if (_entries.TryUpdate(userId, new Entry(updated, DateTimeOffset.UtcNow.Add(ttl)), current))
            {
                return Task.FromResult<UserLiveStatus?>(updated);
            }
        }
        return Task.FromResult<UserLiveStatus?>(null);
    }

    private sealed record Entry(UserLiveStatus Status, DateTimeOffset ExpiresAtUtc);
}
