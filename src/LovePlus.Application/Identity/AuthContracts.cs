namespace LovePlus.Application.Identity;

public sealed record AuthUserDto(Guid Id, string Email, string DisplayName);

public sealed record AuthTokens(
    string AccessToken,
    DateTimeOffset AccessTokenExpiresAtUtc,
    string RefreshToken,
    DateTimeOffset RefreshTokenExpiresAtUtc,
    string TelemetryToken,
    DateTimeOffset TelemetryTokenExpiresAtUtc,
    Guid DeviceSessionId,
    AuthUserDto User);
