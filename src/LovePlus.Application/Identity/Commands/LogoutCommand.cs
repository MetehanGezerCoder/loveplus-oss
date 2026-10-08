using LovePlus.Application.Common.Abstractions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace LovePlus.Application.Identity.Commands;

public sealed record LogoutCommand(Guid UserId, Guid DeviceSessionId) : IRequest;

public sealed class LogoutCommandHandler(IApplicationDbContext db, IClock clock)
    : IRequestHandler<LogoutCommand>
{
    public async Task Handle(LogoutCommand request, CancellationToken ct)
    {
        var session = await db.DeviceSessions
            .Include(x => x.RefreshTokens)
            .SingleOrDefaultAsync(x => x.Id == request.DeviceSessionId && x.UserId == request.UserId, ct);
        if (session is null)
        {
            return;
        }

        var now = clock.UtcNow;
        session.RevokedAtUtc ??= now;
        foreach (var token in session.RefreshTokens.Where(x => x.RevokedAtUtc == null))
        {
            token.RevokedAtUtc = now;
        }
        await db.SaveChangesAsync(ct);
    }
}
