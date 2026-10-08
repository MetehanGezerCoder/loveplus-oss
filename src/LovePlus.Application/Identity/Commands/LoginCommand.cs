using FluentValidation;
using LovePlus.Application.Common.Abstractions;
using LovePlus.Application.Common.Exceptions;
using LovePlus.Domain.Identity;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace LovePlus.Application.Identity.Commands;

public sealed record LoginCommand(
    string Email,
    string Password,
    string DeviceId,
    string DeviceName) : IRequest<AuthTokens>;

public sealed class LoginCommandValidator : AbstractValidator<LoginCommand>
{
    public LoginCommandValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(254);
        RuleFor(x => x.Password).NotEmpty().MaximumLength(128);
        RuleFor(x => x.DeviceId).NotEmpty().MaximumLength(128);
        RuleFor(x => x.DeviceName).NotEmpty().MaximumLength(120);
    }
}

public sealed class LoginCommandHandler(
    IApplicationDbContext db,
    IPasswordService passwords,
    ITokenService tokens,
    IClock clock) : IRequestHandler<LoginCommand, AuthTokens>
{
    public async Task<AuthTokens> Handle(LoginCommand request, CancellationToken ct)
    {
        var normalizedEmail = request.Email.Trim().ToUpperInvariant();
        var user = await db.Users.SingleOrDefaultAsync(
            x => x.NormalizedEmail == normalizedEmail && x.DeletedAtUtc == null, ct);
        if (user is null || !passwords.Verify(user, user.PasswordHash, request.Password))
        {
            throw new AuthenticationException("Invalid email or password.");
        }

        var now = clock.UtcNow;
        var oldSessions = await db.DeviceSessions
            .Where(x => x.UserId == user.Id && x.DeviceId == request.DeviceId && x.RevokedAtUtc == null)
            .Include(x => x.RefreshTokens)
            .ToListAsync(ct);
        foreach (var oldSession in oldSessions)
        {
            oldSession.RevokedAtUtc = now;
            foreach (var oldToken in oldSession.RefreshTokens.Where(x => x.RevokedAtUtc == null))
            {
                oldToken.RevokedAtUtc = now;
            }
        }

        var session = new DeviceSession
        {
            UserId = user.Id,
            DeviceId = request.DeviceId.Trim(),
            DeviceName = request.DeviceName.Trim(),
            CreatedAtUtc = now,
            LastSeenAtUtc = now
        };
        var refreshSecret = tokens.GenerateRefreshToken();
        var refresh = new RefreshToken
        {
            DeviceSessionId = session.Id,
            TokenHash = tokens.HashSecret(refreshSecret),
            CreatedAtUtc = now,
            ExpiresAtUtc = now.Add(tokens.RefreshTokenLifetime)
        };
        db.DeviceSessions.Add(session);
        db.RefreshTokens.Add(refresh);
        await db.SaveChangesAsync(ct);

        return RegisterCommandHandler.CreateResponse(
            user, session, refreshSecret, refresh.ExpiresAtUtc, tokens, now);
    }
}
