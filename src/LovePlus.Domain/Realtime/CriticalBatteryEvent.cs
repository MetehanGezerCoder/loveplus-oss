using NetTopologySuite.Geometries;

namespace LovePlus.Domain.Realtime;

public sealed class CriticalBatteryEvent
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public Guid PairId { get; set; }
    public int BatteryLevel { get; set; }
    public Point? LastKnownLocation { get; set; }
    public DateTimeOffset OccurredAtUtc { get; set; }
    public DateTimeOffset? RecoveredAtUtc { get; set; }
}
