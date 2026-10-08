namespace LovePlus.Domain.Pairing;

public enum CouplePairStatus
{
    Active,
    Ended,
    DeletionPending
}

public sealed class CouplePair
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public CouplePairStatus Status { get; set; } = CouplePairStatus.Active;
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset? EndedAtUtc { get; set; }
    public DateTimeOffset? DeletionRequestedAtUtc { get; set; }
    public ICollection<PairMember> Members { get; set; } = [];
}
