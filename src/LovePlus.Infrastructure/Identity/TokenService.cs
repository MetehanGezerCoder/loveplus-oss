using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using LovePlus.Application.Common.Abstractions;
using LovePlus.Domain.Identity;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace LovePlus.Infrastructure.Identity;

public sealed class TokenService(IOptions<JwtOptions> options) : ITokenService
{
    private static readonly char[] PairingAlphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789".ToCharArray();
    private readonly JwtOptions _options = options.Value;

    public TimeSpan AccessTokenLifetime => TimeSpan.FromMinutes(_options.AccessTokenMinutes);
    public TimeSpan TelemetryTokenLifetime => TimeSpan.FromDays(_options.TelemetryTokenDays);
    public TimeSpan RefreshTokenLifetime => TimeSpan.FromDays(_options.RefreshTokenDays);

    public string CreateAccessToken(User user, DeviceSession session, DateTimeOffset now)
        => CreateToken(user, session, now, AccessTokenLifetime, "app");

    public string CreateTelemetryToken(User user, DeviceSession session, DateTimeOffset now)
        => CreateToken(user, session, now, TelemetryTokenLifetime, "telemetry");

    private string CreateToken(User user, DeviceSession session, DateTimeOffset now, TimeSpan lifetime, string tokenUse)
    {
        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.SigningKey)),
            SecurityAlgorithms.HmacSha256);
        var jwt = new JwtSecurityToken(
            _options.Issuer,
            _options.Audience,
            [
                new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
                new Claim(JwtRegisteredClaimNames.Email, user.Email),
                new Claim("session_id", session.Id.ToString()),
                new Claim("device_id", session.DeviceId),
                new Claim("token_use", tokenUse),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
            ],
            now.UtcDateTime,
            now.Add(lifetime).UtcDateTime,
            credentials);
        return new JwtSecurityTokenHandler().WriteToken(jwt);
    }

    public string GenerateRefreshToken() =>
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));

    public string HashSecret(string secret) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(secret)));

    public string GeneratePairingCode()
    {
        Span<char> code = stackalloc char[8];
        for (var i = 0; i < code.Length; i++)
        {
            code[i] = PairingAlphabet[RandomNumberGenerator.GetInt32(PairingAlphabet.Length)];
        }
        return $"{new string(code[..4])}-{new string(code[4..])}";
    }
}
