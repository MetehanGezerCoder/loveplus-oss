namespace LovePlus.Application.Heartbeat;

public enum HeartbeatAcknowledgementState
{
    Played,
    HapticDisabled,
    Unsupported
}

public enum HeartbeatReservationState
{
    Accepted,
    Duplicate,
    RateLimited
}

public enum HeartbeatAcknowledgementResultState
{
    Accepted,
    Duplicate,
    Expired,
    Forbidden
}

public sealed record HeartbeatPatternDto(
    Guid EventId,
    Guid SenderUserId,
    string SenderDisplayName,
    IReadOnlyList<int> Pattern,
    int TotalDurationMilliseconds,
    DateTimeOffset SentAtUtc,
    DateTimeOffset ExpiresAtUtc,
    int SchemaVersion = 1);

public sealed record HeartbeatAcknowledgementDto(
    Guid EventId,
    HeartbeatAcknowledgementState State,
    DateTimeOffset AcknowledgedAtUtc);

public sealed record SendHeartbeatResult(
    Guid EventId,
    bool Accepted,
    bool Duplicate,
    DateTimeOffset ExpiresAtUtc);

public sealed record AcknowledgeHeartbeatResult(bool Accepted, bool Duplicate);

public sealed record HeartbeatEnvelope(
    Guid EventId,
    Guid PairId,
    Guid SenderUserId,
    Guid ReceiverUserId,
    DateTimeOffset ExpiresAtUtc);

public sealed record HeartbeatAcknowledgementStoreResult(
    HeartbeatAcknowledgementResultState State,
    Guid? SenderUserId = null);

public sealed record HeartbeatPolicy(
    TimeSpan EventTtl,
    TimeSpan RateWindow,
    int MaxEventsPerWindow,
    int MaxPatternEntries,
    int MaxTotalDurationMilliseconds,
    int MaxEntryDurationMilliseconds);

/// <summary>
/// Stores only short-lived routing/idempotency metadata. A heartbeat pattern is
/// intentionally absent from this contract and must never be persisted.
/// </summary>
public interface IEphemeralHeartbeatStore
{
    Task<HeartbeatReservationState> TryReserveAsync(
        HeartbeatEnvelope envelope,
        HeartbeatPolicy policy,
        CancellationToken cancellationToken);

    Task<HeartbeatAcknowledgementStoreResult> TryAcknowledgeAsync(
        Guid eventId,
        Guid receiverUserId,
        HeartbeatAcknowledgementState state,
        DateTimeOffset acknowledgedAtUtc,
        CancellationToken cancellationToken);
}

public interface IRealtimePresenceTracker
{
    void Connected(Guid userId, string connectionId);
    void Disconnected(Guid userId, string connectionId);
    bool IsOnline(Guid userId);
}

public interface IHeartbeatPublisher
{
    Task PublishPatternAsync(Guid receiverUserId, HeartbeatPatternDto pattern, CancellationToken cancellationToken);
    Task PublishAcknowledgementAsync(
        Guid senderUserId,
        HeartbeatAcknowledgementDto acknowledgement,
        CancellationToken cancellationToken);
}
