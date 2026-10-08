using FluentValidation;
using LovePlus.Application.Common.Abstractions;
using LovePlus.Domain.Identity;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace LovePlus.Application.Identity.Commands;

/// <summary>
/// Binds a push registration token to the authenticated device session. The user, session
/// and device are taken from validated claims; a client can never register a token for
/// somebody else.
/// </summary>
public sealed record RegisterPushTokenCommand(
    Guid AuthenticatedUserId,
    Guid AuthenticatedSessionId,
    string Platform,
    string Token) : IRequest<RegisterPushTokenResult>;

public sealed record RegisterPushTokenResult(bool Accepted, DateTimeOffset RegisteredAtUtc);

public sealed class RegisterPushTokenCommandValidator : AbstractValidator<RegisterPushTokenCommand>
{
    private static readonly string[] SupportedPlatforms = ["android"];

    public RegisterPushTokenCommandValidator()
    {
        RuleFor(x => x.AuthenticatedUserId).NotEmpty();
        RuleFor(x => x.AuthenticatedSessionId).NotEmpty();
        RuleFor(x => x.Platform).NotEmpty().Must(x => SupportedPlatforms.Contains(x))
            .WithMessage("Only Android push registrations are supported in this phase.");
        RuleFor(x => x.Token).NotEmpty().MinimumLength(16).MaximumLength(512);
    }
}

public sealed class RegisterPushTokenCommandHandler(IApplicationDbContext db, IClock clock)
    : IRequestHandler<RegisterPushTokenCommand, RegisterPushTokenResult>
{
    public async Task<RegisterPushTokenResult> Handle(RegisterPushTokenCommand request, CancellationToken ct)
    {
        var now = clock.UtcNow;

        // The same physical device can reinstall and receive a new token, and a token can
        // migrate between sessions. Both directions must converge on one active row.
        var conflicting = await db.DevicePushTokens
            .Where(x => x.Token == request.Token && x.DeviceSessionId != request.AuthenticatedSessionId)
            .ToListAsync(ct);
        foreach (var stale in conflicting)
        {
            stale.DisabledAtUtc = now;
            stale.UpdatedAtUtc = now;
            stale.Token = $"revoked:{stale.Id:N}";
        }

        var existing = await db.DevicePushTokens
            .SingleOrDefaultAsync(x => x.DeviceSessionId == request.AuthenticatedSessionId, ct);
        if (existing is null)
        {
            db.DevicePushTokens.Add(new DevicePushToken
            {
                UserId = request.AuthenticatedUserId,
                DeviceSessionId = request.AuthenticatedSessionId,
                Platform = request.Platform,
                Token = request.Token,
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            });
        }
        else
        {
            existing.UserId = request.AuthenticatedUserId;
            existing.Platform = request.Platform;
            existing.Token = request.Token;
            existing.UpdatedAtUtc = now;
            existing.DisabledAtUtc = null;
        }

        await db.SaveChangesAsync(ct);
        return new RegisterPushTokenResult(true, now);
    }
}
