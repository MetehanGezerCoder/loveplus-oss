using LovePlus.Application.Realtime.Commands;
using LovePlus.Domain.Realtime;
using LovePlus.Application.Heartbeat;

namespace LovePlus.Api.Contracts;

public sealed record RegisterRequest(
    string Email,
    string Password,
    string DisplayName,
    string DeviceId,
    string DeviceName);

public sealed record LoginRequest(string Email, string Password, string DeviceId, string DeviceName);
public sealed record RefreshRequest(string RefreshToken, string DeviceId);
public sealed record RedeemPairingCodeRequest(string Code);
public sealed record SendHeartbeatRequest(Guid EventId, IReadOnlyList<int> Pattern);
public sealed record AcknowledgeHeartbeatRequest(HeartbeatAcknowledgementState State);
public sealed record UpdateMoodRequest(MoodType Mood, bool IsMoodSharingEnabled = true);
public sealed record RegisterPushTokenRequest(string Platform, string Token);

public sealed record LocationRequest(
    double Latitude,
    double Longitude,
    double Accuracy,
    double? Speed,
    double? Heading,
    double? Altitude,
    DateTimeOffset RecordedAtUtc);

public sealed record StatusUpdateRequest(
    LocationRequest? Location,
    bool IsLocationSharingEnabled,
    bool ShareLastKnownLocation,
    int BatteryLevel,
    bool IsCharging,
    BatteryState BatteryState,
    ActivityType ActivityType,
    MoodType Mood,
    DateTimeOffset RecordedAtUtc,
    long SequenceNumber,
    TelemetrySource Source = TelemetrySource.RealDevice,
    bool? IsMoodSharingEnabled = null)
{
    public UpdateUserStatusCommand ToCommand(Guid userId, string deviceId) => new(
        userId,
        deviceId,
        Location is null ? null : new LocationInput(
            Location.Latitude,
            Location.Longitude,
            Location.Accuracy,
            Location.Speed,
            Location.Heading,
            Location.Altitude,
            Location.RecordedAtUtc),
        IsLocationSharingEnabled,
        ShareLastKnownLocation,
        BatteryLevel,
        IsCharging,
        BatteryState,
        ActivityType,
        Mood,
        RecordedAtUtc,
        SequenceNumber,
        Source,
        IsMoodSharingEnabled ?? true);
}
