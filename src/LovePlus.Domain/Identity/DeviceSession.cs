namespace LovePlus.Domain.Identity;

public sealed class DeviceSession
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public required string DeviceId { get; set; }
    public required string DeviceName { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset LastSeenAtUtc { get; set; }
    public DateTimeOffset? RevokedAtUtc { get; set; }
    public User User { get; set; } = null!;
    public ICollection<RefreshToken> RefreshTokens { get; set; } = [];
}
