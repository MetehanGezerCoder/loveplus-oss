namespace LovePlus.Domain.Realtime;

public enum ActivityType
{
    Unknown,
    Stationary,
    Walking,
    Running,
    Cycling,
    InVehicle
}

public enum BatteryState
{
    Unknown,
    Charging,
    Discharging,
    Full,
    NotCharging
}

public enum MoodType
{
    None,
    Happy,
    Calm,
    Excited,
    Tired,
    Sad,
    Busy,
    InLove,
    MissingYou,
    Sulky
}

public enum TelemetrySource
{
    RealDevice,
    Simulator
}

public sealed record LocationSample(
    double Latitude,
    double Longitude,
    double Accuracy,
    double? Speed,
    double? Heading,
    double? Altitude,
    DateTimeOffset RecordedAtUtc);

public sealed record UserLiveStatus(
    Guid UserId,
    Guid PairId,
    LocationSample? Location,
    bool IsLocationSharingEnabled,
    bool ShareLastKnownLocation,
    int BatteryLevel,
    bool IsCharging,
    BatteryState BatteryState,
    ActivityType ActivityType,
    MoodType Mood,
    DateTimeOffset RecordedAtUtc,
    string DeviceId,
    long SequenceNumber,
    TelemetrySource Source,
    bool IsMoodSharingEnabled = true)
{
    public bool IsCriticalBattery => BatteryLevel <= 5;
    public long RecordedAtUnixMilliseconds => RecordedAtUtc.ToUnixTimeMilliseconds();
}
