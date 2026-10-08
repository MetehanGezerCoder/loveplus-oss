namespace LovePlus.Domain.Identity;

/// <summary>
/// A push registration token for one device session. The token is a delivery address
/// rather than an authentication secret, but it identifies a private device, so it is
/// never logged and never leaves the pair-scoped notification path.
/// </summary>
public sealed class DevicePushToken
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public Guid DeviceSessionId { get; set; }
    public required string Platform { get; set; }
    public required string Token { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
    public DateTimeOffset? DisabledAtUtc { get; set; }
    public User User { get; set; } = null!;
    public DeviceSession DeviceSession { get; set; } = null!;
}
