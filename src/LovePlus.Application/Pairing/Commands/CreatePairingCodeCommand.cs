using LovePlus.Application.Common.Abstractions;
using LovePlus.Application.Common.Exceptions;
using LovePlus.Domain.Pairing;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace LovePlus.Application.Pairing.Commands;

public sealed record CreatePairingCodeCommand(Guid UserId) : IRequest<PairingCodeDto>;

public sealed class CreatePairingCodeCommandHandler(
    IApplicationDbContext db,
    ITokenService tokens,
    IClock clock) : IRequestHandler<CreatePairingCodeCommand, PairingCodeDto>
{
    public async Task<PairingCodeDto> Handle(CreatePairingCodeCommand request, CancellationToken ct)
    {
        if (await db.PairMembers.AnyAsync(x => x.UserId == request.UserId && x.IsActive, ct))
        {
            throw new ConflictException("User already belongs to an active pair.");
        }

        var now = clock.UtcNow;
        var previousCodes = await db.PairingCodes
            .Where(x => x.CreatorUserId == request.UserId && x.ConsumedAtUtc == null && x.ExpiresAtUtc > now)
            .ToListAsync(ct);
        foreach (var previous in previousCodes)
        {
            previous.ExpiresAtUtc = now;
        }

        var code = tokens.GeneratePairingCode();
        var entity = new PairingCode
        {
            CreatorUserId = request.UserId,
            CodeHash = tokens.HashSecret(Normalize(code)),
            CreatedAtUtc = now,
            ExpiresAtUtc = now.AddMinutes(10)
        };
        db.PairingCodes.Add(entity);
        await db.SaveChangesAsync(ct);
        return new PairingCodeDto(code, entity.ExpiresAtUtc);
    }

    internal static string Normalize(string code) =>
        code.Replace("-", string.Empty, StringComparison.Ordinal).Trim().ToUpperInvariant();
}
