using NetTopologySuite.Geometries;

namespace LovePlus.Domain.Realtime;

public sealed class UserLiveStatusSnapshot
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public Guid PairId { get; set; }
    public Point? Location { get; set; }
    public double? Accuracy { get; set; }
    public double? Speed { get; set; }
    public double? Heading { get; set; }
    public double? Altitude { get; set; }
    public DateTimeOffset? LocationRecordedAtUtc { get; set; }
    public bool IsLocationSharingEnabled { get; set; }
    public bool ShareLastKnownLocation { get; set; }
    public int BatteryLevel { get; set; }
    public bool IsCharging { get; set; }
    public BatteryState BatteryState { get; set; }
    public ActivityType ActivityType { get; set; }
    public MoodType Mood { get; set; }
    public bool IsMoodSharingEnabled { get; set; } = true;
    public DateTimeOffset RecordedAtUtc { get; set; }
    public DateTimeOffset PersistedAtUtc { get; set; }
    public required string DeviceId { get; set; }
    public long SequenceNumber { get; set; }
    public bool IsCriticalBatteryLocation { get; set; }
    public TelemetrySource Source { get; set; }
}
