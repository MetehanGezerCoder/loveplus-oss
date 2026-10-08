using FluentValidation;
using LovePlus.Application.Common.Abstractions;
using LovePlus.Application.Common.Exceptions;
using LovePlus.Domain.Identity;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace LovePlus.Application.Identity.Commands;

public sealed record RegisterCommand(
    string Email,
    string Password,
    string DisplayName,
    string DeviceId,
    string DeviceName) : IRequest<AuthTokens>;

public sealed class RegisterCommandValidator : AbstractValidator<RegisterCommand>
{
    public RegisterCommandValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(254);
        RuleFor(x => x.Password).MinimumLength(10).MaximumLength(128);
        RuleFor(x => x.DisplayName).NotEmpty().MaximumLength(80);
        RuleFor(x => x.DeviceId).NotEmpty().MaximumLength(128);
        RuleFor(x => x.DeviceName).NotEmpty().MaximumLength(120);
    }
}

public sealed class RegisterCommandHandler(
    IApplicationDbContext db,
    IPasswordService passwords,
    ITokenService tokens,
    IClock clock) : IRequestHandler<RegisterCommand, AuthTokens>
{
    public async Task<AuthTokens> Handle(RegisterCommand request, CancellationToken ct)
    {
        var normalizedEmail = request.Email.Trim().ToUpperInvariant();
        if (await db.Users.AnyAsync(x => x.NormalizedEmail == normalizedEmail, ct))
        {
            throw new ConflictException("An account with this email already exists.");
        }

        var now = clock.UtcNow;
        var user = new User
        {
            Email = request.Email.Trim().ToLowerInvariant(),
            NormalizedEmail = normalizedEmail,
            DisplayName = request.DisplayName.Trim(),
            PasswordHash = string.Empty,
            CreatedAtUtc = now
        };
        user.PasswordHash = passwords.Hash(user, request.Password);

        var session = new DeviceSession
        {
            User = user,
            UserId = user.Id,
            DeviceId = request.DeviceId.Trim(),
            DeviceName = request.DeviceName.Trim(),
            CreatedAtUtc = now,
            LastSeenAtUtc = now
        };
        var refreshSecret = tokens.GenerateRefreshToken();
        var refresh = new RefreshToken
        {
            DeviceSession = session,
            DeviceSessionId = session.Id,
            TokenHash = tokens.HashSecret(refreshSecret),
            CreatedAtUtc = now,
            ExpiresAtUtc = now.Add(tokens.RefreshTokenLifetime)
        };

        db.Users.Add(user);
        db.DeviceSessions.Add(session);
        db.RefreshTokens.Add(refresh);
        await db.SaveChangesAsync(ct);

        return CreateResponse(user, session, refreshSecret, refresh.ExpiresAtUtc, tokens, now);
    }

    internal static AuthTokens CreateResponse(
        User user,
        DeviceSession session,
        string refreshSecret,
        DateTimeOffset refreshExpiresAt,
        ITokenService tokens,
        DateTimeOffset now) => new(
            tokens.CreateAccessToken(user, session, now),
            now.Add(tokens.AccessTokenLifetime),
            refreshSecret,
            refreshExpiresAt,
            tokens.CreateTelemetryToken(user, session, now),
            now.Add(tokens.TelemetryTokenLifetime),
            session.Id,
            new AuthUserDto(user.Id, user.Email, user.DisplayName));
}
