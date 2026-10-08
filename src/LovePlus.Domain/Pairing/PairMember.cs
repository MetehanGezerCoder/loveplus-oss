using LovePlus.Domain.Identity;

namespace LovePlus.Domain.Pairing;

public sealed class PairMember
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid PairId { get; set; }
    public Guid UserId { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTimeOffset JoinedAtUtc { get; set; }
    public DateTimeOffset? LeftAtUtc { get; set; }
    public CouplePair Pair { get; set; } = null!;
    public User User { get; set; } = null!;
}
