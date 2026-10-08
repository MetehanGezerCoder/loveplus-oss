using LovePlus.Application.Common.Abstractions;
using LovePlus.Application.Common.Exceptions;
using LovePlus.Domain.Realtime;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace LovePlus.Application.Realtime.Queries;

public sealed record GetPartnerStatusQuery(Guid AuthenticatedUserId) : IRequest<PartnerStatusDto?>;

public sealed class GetPartnerStatusQueryHandler(
    IApplicationDbContext db,
    ILiveStatusStore store,
    IGeoDistanceCalculator distances,
    RealtimeStatusPolicy policy,
    IClock clock) : IRequestHandler<GetPartnerStatusQuery, PartnerStatusDto?>
{
    public async Task<PartnerStatusDto?> Handle(GetPartnerStatusQuery request, CancellationToken ct)
    {
        var membership = await db.PairMembers.AsNoTracking()
            .SingleOrDefaultAsync(x => x.UserId == request.AuthenticatedUserId && x.IsActive, ct)
            ?? throw new ForbiddenException("An active pair is required to read partner status.");
        var partner = await db.PairMembers.AsNoTracking()
            .Where(x => x.PairId == membership.PairId && x.UserId != request.AuthenticatedUserId && x.IsActive)
            .Select(x => new { x.UserId, x.User.DisplayName })
            .SingleAsync(ct);

        var partnerLive = await store.GetAsync(partner.UserId, ct);
        var ownLive = await store.GetAsync(request.AuthenticatedUserId, ct);
        if (partnerLive is not null)
        {
            return PartnerStatusMapper.Map(partnerLive, partner.DisplayName, ownLive, policy, distances, clock.UtcNow);
        }

        var snapshot = await db.UserLiveStatusSnapshots.AsNoTracking()
            .SingleOrDefaultAsync(x => x.UserId == partner.UserId && x.PairId == membership.PairId, ct);
        if (snapshot is null)
        {
            return null;
        }

        LocationDto? location = null;
        if (snapshot.Location is not null && (snapshot.IsLocationSharingEnabled || snapshot.ShareLastKnownLocation))
        {
            location = new LocationDto(
                snapshot.Location.Y,
                snapshot.Location.X,
                snapshot.Accuracy ?? 0,
                snapshot.Speed,
                snapshot.Heading,
                snapshot.Altitude,
                snapshot.LocationRecordedAtUtc ?? snapshot.RecordedAtUtc,
                !snapshot.IsLocationSharingEnabled,
                (snapshot.Accuracy ?? 0) >= policy.ApproximateAccuracyMeters);
        }
        var snapshotStatus = new UserLiveStatus(
            snapshot.UserId,
            snapshot.PairId,
            null,
            snapshot.IsLocationSharingEnabled,
            snapshot.ShareLastKnownLocation,
            snapshot.BatteryLevel,
            snapshot.IsCharging,
            snapshot.BatteryState,
            snapshot.ActivityType,
            snapshot.Mood,
            snapshot.RecordedAtUtc,
            snapshot.DeviceId,
            snapshot.SequenceNumber,
            snapshot.Source);
        return PartnerStatusMapper.Map(
            snapshotStatus,
            partner.DisplayName,
            ownLive,
            policy,
            distances,
            clock.UtcNow,
            location,
            true);
    }
}
