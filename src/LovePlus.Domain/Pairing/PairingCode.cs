using LovePlus.Domain.Identity;

namespace LovePlus.Domain.Pairing;

public sealed class PairingCode
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid CreatorUserId { get; set; }
    public required string CodeHash { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset ExpiresAtUtc { get; set; }
    public Guid? ConsumedByUserId { get; set; }
    public DateTimeOffset? ConsumedAtUtc { get; set; }
    public Guid? CouplePairId { get; set; }
    public User CreatorUser { get; set; } = null!;
    public User? ConsumedByUser { get; set; }
    public CouplePair? CouplePair { get; set; }
}
