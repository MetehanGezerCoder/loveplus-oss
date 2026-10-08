using LovePlus.Application.Common.Abstractions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace LovePlus.Application.Pairing.Queries;

public sealed record GetCurrentPairQuery(Guid UserId) : IRequest<PairDto?>;

public sealed class GetCurrentPairQueryHandler(IApplicationDbContext db)
    : IRequestHandler<GetCurrentPairQuery, PairDto?>
{
    public async Task<PairDto?> Handle(GetCurrentPairQuery request, CancellationToken ct)
    {
        var membership = await db.PairMembers.AsNoTracking()
            .SingleOrDefaultAsync(x => x.UserId == request.UserId && x.IsActive, ct);
        if (membership is null)
        {
            return null;
        }

        return await db.PairMembers.AsNoTracking()
            .Where(x => x.PairId == membership.PairId && x.UserId != request.UserId && x.IsActive)
            .Select(x => new PairDto(x.PairId, x.UserId, x.User.DisplayName, x.Pair.CreatedAtUtc))
            .SingleAsync(ct);
    }
}
