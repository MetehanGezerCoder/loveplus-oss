using FluentValidation;
using LovePlus.Application.Common.Abstractions;
using LovePlus.Application.Common.Exceptions;
using LovePlus.Domain.Identity;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace LovePlus.Application.Identity.Commands;

public sealed record RefreshTokenCommand(string RefreshToken, string DeviceId) : IRequest<AuthTokens>;

public sealed class RefreshTokenCommandValidator : AbstractValidator<RefreshTokenCommand>
{
    public RefreshTokenCommandValidator()
    {
        RuleFor(x => x.RefreshToken).NotEmpty().MaximumLength(512);
        RuleFor(x => x.DeviceId).NotEmpty().MaximumLength(128);
    }
}

public sealed class RefreshTokenCommandHandler(
    IApplicationDbContext db,
    ITokenService tokens,
    IClock clock) : IRequestHandler<RefreshTokenCommand, AuthTokens>
{
    public async Task<AuthTokens> Handle(RefreshTokenCommand request, CancellationToken ct)
    {
        var now = clock.UtcNow;
        var hash = tokens.HashSecret(request.RefreshToken);
        var current = await db.RefreshTokens
            .Include(x => x.DeviceSession)
            .ThenInclude(x => x.User)
            .SingleOrDefaultAsync(x => x.TokenHash == hash, ct);
        if (current is null || !string.Equals(current.DeviceSession.DeviceId, request.DeviceId, StringComparison.Ordinal))
        {
            throw new AuthenticationException("Invalid refresh token.");
        }

        var session = current.DeviceSession;
        if (current.RevokedAtUtc is not null)
        {
            session.RevokedAtUtc ??= now;
            var activeTokens = await db.RefreshTokens
                .Where(x => x.DeviceSessionId == session.Id && x.RevokedAtUtc == null)
                .ToListAsync(ct);
            foreach (var token in activeTokens)
            {
                token.RevokedAtUtc = now;
            }
            await db.SaveChangesAsync(ct);
            throw new AuthenticationException("Refresh token reuse detected; the device session was revoked.");
        }

        if (session.RevokedAtUtc is not null || current.ExpiresAtUtc <= now)
        {
            current.RevokedAtUtc ??= now;
            await db.SaveChangesAsync(ct);
            throw new AuthenticationException("Refresh token expired or session revoked.");
        }

        var newSecret = tokens.GenerateRefreshToken();
        var replacement = new RefreshToken
        {
            DeviceSessionId = session.Id,
            TokenHash = tokens.HashSecret(newSecret),
            CreatedAtUtc = now,
            ExpiresAtUtc = now.Add(tokens.RefreshTokenLifetime)
        };
        current.RevokedAtUtc = now;
        current.ReplacedByTokenId = replacement.Id;
        session.LastSeenAtUtc = now;
        db.RefreshTokens.Add(replacement);
        await db.SaveChangesAsync(ct);

        return RegisterCommandHandler.CreateResponse(
            session.User, session, newSecret, replacement.ExpiresAtUtc, tokens, now);
    }
}
