using FluentValidation;
using LovePlus.Application.Common.Abstractions;
using LovePlus.Application.Common.Exceptions;
using LovePlus.Domain.Realtime;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace LovePlus.Application.Realtime.Commands;

public sealed record UpdateMoodCommand(
    Guid AuthenticatedUserId,
    MoodType Mood,
    bool IsMoodSharingEnabled) : IRequest<MoodUpdateResult>;

public sealed class UpdateMoodCommandValidator : AbstractValidator<UpdateMoodCommand>
{
    public UpdateMoodCommandValidator()
    {
        RuleFor(x => x.AuthenticatedUserId).NotEmpty();
        RuleFor(x => x.Mood).IsInEnum();
    }
}

public sealed class UpdateMoodCommandHandler(
    IApplicationDbContext db,
    ILiveStatusStore store,
    IPartnerStatusPublisher publisher,
    IGeoDistanceCalculator distances,
    RealtimeStatusPolicy policy,
    IClock clock) : IRequestHandler<UpdateMoodCommand, MoodUpdateResult>
{
    public async Task<MoodUpdateResult> Handle(UpdateMoodCommand request, CancellationToken ct)
    {
        var membership = await db.PairMembers.AsNoTracking()
            .Where(x => x.UserId == request.AuthenticatedUserId && x.IsActive)
            .Select(x => new { x.PairId })
            .SingleOrDefaultAsync(ct)
            ?? throw new ForbiddenException("An active pair is required to share a mood.");
        var name = await db.Users.AsNoTracking()
            .Where(x => x.Id == request.AuthenticatedUserId)
            .Select(x => x.DisplayName)
            .SingleAsync(ct);
        var partnerId = await db.PairMembers.AsNoTracking()
            .Where(x => x.PairId == membership.PairId && x.UserId != request.AuthenticatedUserId && x.IsActive)
            .Select(x => x.UserId)
            .SingleAsync(ct);

        var previous = await store.GetAsync(request.AuthenticatedUserId, ct)
            ?? throw new ConflictException("Live device status must be active before sharing a mood.");
        if (previous.Mood == request.Mood && previous.IsMoodSharingEnabled == request.IsMoodSharingEnabled)
        {
            return new MoodUpdateResult(true, false, clock.UtcNow);
        }

        var updated = await store.UpdateMoodAsync(
            request.AuthenticatedUserId,
            request.Mood,
            request.IsMoodSharingEnabled,
            policy.LiveStatusTtl,
            ct) ?? throw new ConflictException("Live device status expired while updating mood.");

        var snapshot = await db.UserLiveStatusSnapshots
            .SingleOrDefaultAsync(x => x.UserId == request.AuthenticatedUserId, ct);
        if (snapshot is not null)
        {
            snapshot.Mood = request.Mood;
            snapshot.IsMoodSharingEnabled = request.IsMoodSharingEnabled;
            snapshot.PersistedAtUtc = clock.UtcNow;
            await db.SaveChangesAsync(ct);
        }

        var partnerLive = await store.GetAsync(partnerId, ct);
        var dto = PartnerStatusMapper.Map(updated, name, partnerLive, policy, distances, clock.UtcNow);
        await publisher.PublishStatusAsync(membership.PairId, dto, ct);
        return new MoodUpdateResult(true, true, clock.UtcNow);
    }
}
