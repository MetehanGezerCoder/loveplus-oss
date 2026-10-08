using System.Collections.Concurrent;
using LovePlus.Application.Heartbeat;

namespace LovePlus.Infrastructure.Realtime;

public sealed class InMemoryHeartbeatStore : IEphemeralHeartbeatStore
{
    private readonly object _gate = new();
    private readonly Dictionary<Guid, Entry> _events = [];
    private readonly Dictionary<Guid, Queue<DateTimeOffset>> _senderWindows = [];
    private readonly Dictionary<Guid, Queue<DateTimeOffset>> _pairWindows = [];

    public Task<HeartbeatReservationState> TryReserveAsync(
        HeartbeatEnvelope envelope,
        HeartbeatPolicy policy,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
        {
            RemoveExpired(envelope.ExpiresAtUtc.Subtract(policy.EventTtl));
            if (_events.ContainsKey(envelope.EventId))
            {
                return Task.FromResult(HeartbeatReservationState.Duplicate);
            }

            var now = envelope.ExpiresAtUtc.Subtract(policy.EventTtl);
            var senderWindow = Window(_senderWindows, envelope.SenderUserId, now, policy.RateWindow);
            var pairWindow = Window(_pairWindows, envelope.PairId, now, policy.RateWindow);
            if (senderWindow.Count >= policy.MaxEventsPerWindow
                || pairWindow.Count >= policy.MaxEventsPerWindow)
            {
                return Task.FromResult(HeartbeatReservationState.RateLimited);
            }

            senderWindow.Enqueue(now);
            pairWindow.Enqueue(now);
            _events[envelope.EventId] = new Entry(envelope, false);
            return Task.FromResult(HeartbeatReservationState.Accepted);
        }
    }

    private static Queue<DateTimeOffset> Window(
        Dictionary<Guid, Queue<DateTimeOffset>> windows,
        Guid key,
        DateTimeOffset now,
        TimeSpan rateWindow)
    {
        if (!windows.TryGetValue(key, out var window))
        {
            window = new Queue<DateTimeOffset>();
            windows[key] = window;
        }
        while (window.TryPeek(out var sentAt) && now - sentAt >= rateWindow)
        {
            window.Dequeue();
        }
        return window;
    }

    public Task<HeartbeatAcknowledgementStoreResult> TryAcknowledgeAsync(
        Guid eventId,
        Guid receiverUserId,
        HeartbeatAcknowledgementState state,
        DateTimeOffset acknowledgedAtUtc,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
        {
            RemoveExpired(acknowledgedAtUtc);
            if (!_events.TryGetValue(eventId, out var entry))
            {
                return Task.FromResult(new HeartbeatAcknowledgementStoreResult(
                    HeartbeatAcknowledgementResultState.Expired));
            }
            if (entry.Envelope.ReceiverUserId != receiverUserId)
            {
                return Task.FromResult(new HeartbeatAcknowledgementStoreResult(
                    HeartbeatAcknowledgementResultState.Forbidden));
            }
            if (entry.Acknowledged)
            {
                return Task.FromResult(new HeartbeatAcknowledgementStoreResult(
                    HeartbeatAcknowledgementResultState.Duplicate,
                    entry.Envelope.SenderUserId));
            }

            _events[eventId] = entry with { Acknowledged = true };
            return Task.FromResult(new HeartbeatAcknowledgementStoreResult(
                HeartbeatAcknowledgementResultState.Accepted,
                entry.Envelope.SenderUserId));
        }
    }

    private void RemoveExpired(DateTimeOffset now)
    {
        foreach (var key in _events.Where(x => x.Value.Envelope.ExpiresAtUtc <= now).Select(x => x.Key).ToArray())
        {
            _events.Remove(key);
        }
    }

    private sealed record Entry(HeartbeatEnvelope Envelope, bool Acknowledged);
}

public sealed class InMemoryRealtimePresenceTracker : IRealtimePresenceTracker
{
    private readonly ConcurrentDictionary<Guid, ConcurrentDictionary<string, byte>> _connections = new();

    public void Connected(Guid userId, string connectionId) =>
        _connections.GetOrAdd(userId, _ => new ConcurrentDictionary<string, byte>())[connectionId] = 0;

    public void Disconnected(Guid userId, string connectionId)
    {
        if (!_connections.TryGetValue(userId, out var connections)) return;
        connections.TryRemove(connectionId, out _);
        if (connections.IsEmpty)
        {
            _connections.TryRemove(new KeyValuePair<Guid, ConcurrentDictionary<string, byte>>(userId, connections));
        }
    }

    public bool IsOnline(Guid userId) =>
        _connections.TryGetValue(userId, out var connections) && !connections.IsEmpty;
}
