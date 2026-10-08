using LovePlus.Domain.Realtime;

namespace LovePlus.Application.Realtime;

public static class PartnerStatusMapper
{
    public static PartnerStatusDto Map(
        UserLiveStatus subject,
        string subjectName,
        UserLiveStatus? counterpart,
        RealtimeStatusPolicy policy,
        IGeoDistanceCalculator distances,
        DateTimeOffset now,
        LocationDto? snapshotLocation = null,
        bool isSnapshot = false)
    {
        var subjectLocation = subject.Location is null
            ? snapshotLocation
            : ToLocation(subject.Location, false, policy);
        if (!subject.IsLocationSharingEnabled)
        {
            // Retaining a last-known sample internally can support safety workflows,
            // but location sharing off must never expose old exact coordinates.
            subjectLocation = null;
        }

        var counterpartLocation = counterpart?.Location is null
            ? null
            : ToLocation(counterpart.Location, false, policy);
        if (counterpart is not null && !counterpart.IsLocationSharingEnabled && !counterpart.ShareLastKnownLocation)
        {
            counterpartLocation = null;
        }

        var distanceAvailability = DistanceAvailability.Available;
        double? distance = null;
        if (!subject.IsLocationSharingEnabled)
            distanceAvailability = DistanceAvailability.PartnerSharingOff;
        else if (subjectLocation is null)
            distanceAvailability = DistanceAvailability.PartnerLocationUnavailable;
        else if (now - subjectLocation.RecordedAtUtc > policy.LocationFreshnessWindow)
            distanceAvailability = DistanceAvailability.PartnerLocationStale;
        else if (counterpart is not null && !counterpart.IsLocationSharingEnabled)
            distanceAvailability = DistanceAvailability.OwnSharingOff;
        else if (counterpartLocation is null)
            distanceAvailability = DistanceAvailability.OwnLocationUnavailable;
        else if (now - counterpartLocation.RecordedAtUtc > policy.LocationFreshnessWindow)
            distanceAvailability = DistanceAvailability.OwnLocationStale;
        else
            distance = distances.CalculateMeters(
                subjectLocation.Latitude,
                subjectLocation.Longitude,
                counterpartLocation.Latitude,
                counterpartLocation.Longitude);

        var proximity = policy.Proximity(
            distance,
            subjectLocation?.Accuracy ?? 0,
            counterpartLocation?.Accuracy ?? 0);

        return new PartnerStatusDto(
            subject.UserId,
            subjectName,
            subject.BatteryLevel,
            subject.IsCharging,
            subject.BatteryState,
            subject.ActivityType,
            subject.IsMoodSharingEnabled ? subject.Mood : MoodType.None,
            policy.Presence(subject.RecordedAtUtc, now),
            distance,
            distance is not null && (subjectLocation!.IsApproximate || counterpartLocation!.IsApproximate),
            distanceAvailability,
            subjectLocation,
            counterpartLocation,
            subject.IsLocationSharingEnabled,
            subject.ShareLastKnownLocation,
            subject.RecordedAtUtc,
            subject.Source == TelemetrySource.Simulator,
            proximity,
            subject.IsMoodSharingEnabled,
            (int)policy.OnlineWindow.TotalSeconds,
            (int)policy.RecentlyOnlineWindow.TotalSeconds);
    }

    public static LocationDto ToLocation(LocationSample location, bool isLastKnown, RealtimeStatusPolicy policy) => new(
        location.Latitude,
        location.Longitude,
        location.Accuracy,
        location.Speed,
        location.Heading,
        location.Altitude,
        location.RecordedAtUtc,
        isLastKnown,
        location.Accuracy >= policy.ApproximateAccuracyMeters);
}
