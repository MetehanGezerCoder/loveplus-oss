using LovePlus.Domain.Realtime;

namespace LovePlus.Application.Realtime;

public enum PresenceState
{
    Online,
    RecentlyOnline,
    Offline
}

public enum DistanceAvailability
{
    Available,
    PartnerLocationUnavailable,
    OwnLocationUnavailable,
    PartnerLocationStale,
    OwnLocationStale,
    PartnerSharingOff,
    OwnSharingOff
}

public enum ProximityLevel
{
    Unavailable,
    SamePlace,
    VeryClose,
    Nearby,
    SameArea,
    Far
}

public sealed record LocationDto(
    double Latitude,
    double Longitude,
    double Accuracy,
    double? Speed,
    double? Heading,
    double? Altitude,
    DateTimeOffset RecordedAtUtc,
    bool IsLastKnown,
    bool IsApproximate);

public sealed record PartnerStatusDto(
    Guid UserId,
    string PartnerDisplayName,
    int BatteryLevel,
    bool IsCharging,
    BatteryState BatteryState,
    ActivityType ActivityType,
    MoodType Mood,
    PresenceState Presence,
    double? DistanceMeters,
    bool DistanceIsApproximate,
    DistanceAvailability DistanceAvailability,
    LocationDto? Location,
    LocationDto? CounterpartLocation,
    bool IsLocationSharingEnabled,
    bool ShareLastKnownLocation,
    DateTimeOffset RecordedAtUtc,
    bool IsSimulated,
    ProximityLevel ProximityLevel = ProximityLevel.Unavailable,
    bool IsMoodSharingEnabled = true,
    // The client caches this DTO between pushes, so it must be able to re-derive presence
    // from RecordedAtUtc instead of repeating a verdict that was only true when it was sent.
    int PresenceOnlineSeconds = 180,
    int PresenceRecentlyOnlineSeconds = 600);

public sealed record CriticalBatteryDto(
    Guid EventId,
    Guid UserId,
    int BatteryLevel,
    bool LastKnownLocationAvailable,
    DateTimeOffset OccurredAtUtc);

public sealed record UserStatusResult(
    bool Accepted,
    bool CriticalBatteryEventCreated,
    DateTimeOffset ServerTimeUtc);

public sealed record MoodUpdateResult(bool Accepted, bool Changed, DateTimeOffset ServerTimeUtc);

public sealed record RealtimeStatusPolicy(
    TimeSpan LiveStatusTtl,
    TimeSpan OnlineWindow,
    TimeSpan RecentlyOnlineWindow,
    TimeSpan LocationFreshnessWindow,
    TimeSpan MaxOfflinePacketAge,
    double ApproximateAccuracyMeters,
    bool SimulatorEnabled,
    double SamePlaceMaxMeters = 100,
    double VeryCloseMaxMeters = 500,
    double NearbyMaxMeters = 2_000,
    double SameAreaMaxMeters = 10_000)
{
    public PresenceState Presence(DateTimeOffset recordedAt, DateTimeOffset now)
    {
        var age = now - recordedAt;
        if (age <= OnlineWindow) return PresenceState.Online;
        return age <= RecentlyOnlineWindow ? PresenceState.RecentlyOnline : PresenceState.Offline;
    }


    public ProximityLevel Proximity(
        double? distanceMeters,
        double subjectAccuracyMeters,
        double counterpartAccuracyMeters)
    {
        if (distanceMeters is null) return ProximityLevel.Unavailable;

        var distance = distanceMeters.Value;
        if (distance <= SamePlaceMaxMeters)
        {
            // Accuracy radii crossing the configured boundary cannot prove that two
            // people are in the same place, so deliberately downgrade the wording.
            var uncertainty = Math.Max(0, subjectAccuracyMeters) + Math.Max(0, counterpartAccuracyMeters);
            return distance + uncertainty <= SamePlaceMaxMeters
                ? ProximityLevel.SamePlace
                : ProximityLevel.VeryClose;
        }
        if (distance <= VeryCloseMaxMeters) return ProximityLevel.VeryClose;
        if (distance <= NearbyMaxMeters) return ProximityLevel.Nearby;
        if (distance <= SameAreaMaxMeters) return ProximityLevel.SameArea;
        return ProximityLevel.Far;
    }
}

public interface ILiveStatusStore
{
    Task<UserLiveStatus?> GetAsync(Guid userId, CancellationToken cancellationToken);
    Task<bool> TrySetAsync(UserLiveStatus status, TimeSpan ttl, CancellationToken cancellationToken);
    Task<UserLiveStatus?> UpdateMoodAsync(
        Guid userId,
        MoodType mood,
        bool isMoodSharingEnabled,
        TimeSpan ttl,
        CancellationToken cancellationToken);
}

public interface IPartnerStatusPublisher
{
    Task PublishStatusAsync(Guid pairId, PartnerStatusDto status, CancellationToken cancellationToken);
    Task PublishCriticalBatteryAsync(Guid pairId, CriticalBatteryDto criticalBattery, CancellationToken cancellationToken);
}

public interface IPushNotificationSender
{
    Task SendCriticalBatteryAsync(Guid partnerUserId, CriticalBatteryDto notification, CancellationToken cancellationToken);
}

public interface IGeoDistanceCalculator
{
    double CalculateMeters(double latitude1, double longitude1, double latitude2, double longitude2);
}
