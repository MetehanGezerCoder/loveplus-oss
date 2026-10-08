using System.Data;
using FluentValidation;
using LovePlus.Application.Common.Abstractions;
using LovePlus.Application.Common.Exceptions;
using LovePlus.Domain.Pairing;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace LovePlus.Application.Pairing.Commands;

public sealed record RedeemPairingCodeCommand(Guid UserId, string Code) : IRequest<PairDto>;

public sealed class RedeemPairingCodeCommandValidator : AbstractValidator<RedeemPairingCodeCommand>
{
    public RedeemPairingCodeCommandValidator() =>
        RuleFor(x => x.Code).NotEmpty().MaximumLength(16);
}

public sealed class RedeemPairingCodeCommandHandler(
    IApplicationDbContext db,
    ITokenService tokens,
    IClock clock) : IRequestHandler<RedeemPairingCodeCommand, PairDto>
{
    public Task<PairDto> Handle(RedeemPairingCodeCommand request, CancellationToken ct) =>
        // Production enables Npgsql retry-on-failure, and EF Core refuses a manually opened
        // transaction under a retrying execution strategy (it cannot safely replay a user
        // transaction on retry) — ExecuteInTransactionAsync is the strategy-aware equivalent.
        db.ExecuteInTransactionAsync(IsolationLevel.Serializable, token => HandleInTransaction(request, token), ct);

    private async Task<PairDto> HandleInTransaction(RedeemPairingCodeCommand request, CancellationToken ct)
    {
        var now = clock.UtcNow;
        var hash = tokens.HashSecret(CreatePairingCodeCommandHandler.Normalize(request.Code));
        var code = await db.PairingCodes
            .Include(x => x.CreatorUser)
            .SingleOrDefaultAsync(x => x.CodeHash == hash, ct);

        if (code is null || code.ExpiresAtUtc <= now)
        {
            throw new NotFoundException("Pairing code is invalid or expired.");
        }
        if (code.ConsumedAtUtc is not null)
        {
            throw new ConflictException("Pairing code has already been used.");
        }
        if (code.CreatorUserId == request.UserId)
        {
            throw new ConflictException("A pairing code cannot be redeemed by its creator.");
        }

        var memberIds = new[] { code.CreatorUserId, request.UserId };
        if (await db.PairMembers.AnyAsync(x => memberIds.Contains(x.UserId) && x.IsActive, ct))
        {
            throw new ConflictException("One of the users already belongs to an active pair.");
        }

        var redeemer = await db.Users.SingleOrDefaultAsync(
            x => x.Id == request.UserId && x.DeletedAtUtc == null, ct)
            ?? throw new NotFoundException("User not found.");
        var pair = new CouplePair { CreatedAtUtc = now };
        pair.Members.Add(new PairMember
        {
            Pair = pair,
            PairId = pair.Id,
            UserId = code.CreatorUserId,
            JoinedAtUtc = now
        });
        pair.Members.Add(new PairMember
        {
            Pair = pair,
            PairId = pair.Id,
            UserId = request.UserId,
            JoinedAtUtc = now
        });
        code.ConsumedByUserId = request.UserId;
        code.ConsumedAtUtc = now;
        code.CouplePairId = pair.Id;
        db.CouplePairs.Add(pair);
        await db.SaveChangesAsync(ct);

        _ = redeemer;
        return new PairDto(pair.Id, code.CreatorUserId, code.CreatorUser.DisplayName, now);
    }
}
