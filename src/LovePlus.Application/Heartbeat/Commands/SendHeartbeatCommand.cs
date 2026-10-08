using FluentValidation;
using LovePlus.Application.Common.Abstractions;
using LovePlus.Application.Common.Exceptions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace LovePlus.Application.Heartbeat.Commands;

public sealed record SendHeartbeatCommand(
    Guid AuthenticatedUserId,
    Guid EventId,
    IReadOnlyList<int> Pattern) : IRequest<SendHeartbeatResult>;

public sealed class SendHeartbeatCommandValidator : AbstractValidator<SendHeartbeatCommand>
{
    public SendHeartbeatCommandValidator(HeartbeatPolicy policy)
    {
        RuleFor(x => x.AuthenticatedUserId).NotEmpty();
        RuleFor(x => x.EventId).NotEmpty();
        RuleFor(x => x.Pattern)
            .Cascade(CascadeMode.Stop)
            .NotNull()
            .Must(x => x.Count is >= 2 && x.Count <= policy.MaxPatternEntries)
            .WithMessage($"Pattern must contain between 2 and {policy.MaxPatternEntries} entries.")
            .Must(x => x.Count > 0 && x[0] == 0)
            .WithMessage("Pattern must begin with a zero-delay entry.")
            .Must(x => x.Skip(1).All(duration => duration is >= 30 && duration <= policy.MaxEntryDurationMilliseconds))
            .WithMessage($"Pattern entries must be between 30 and {policy.MaxEntryDurationMilliseconds} ms.")
            .Must(x => x.Sum() is > 0 && x.Sum() <= policy.MaxTotalDurationMilliseconds)
            .WithMessage($"Pattern duration cannot exceed {policy.MaxTotalDurationMilliseconds} ms.");
    }
}

public sealed class SendHeartbeatCommandHandler(
    IApplicationDbContext db,
    IEphemeralHeartbeatStore store,
    IRealtimePresenceTracker presence,
    IHeartbeatPublisher publisher,
    HeartbeatPolicy policy,
    IClock clock) : IRequestHandler<SendHeartbeatCommand, SendHeartbeatResult>
{
    public async Task<SendHeartbeatResult> Handle(SendHeartbeatCommand request, CancellationToken ct)
    {
        var membership = await db.PairMembers.AsNoTracking()
            .Where(x => x.UserId == request.AuthenticatedUserId && x.IsActive)
            .Select(x => new { x.PairId })
            .SingleOrDefaultAsync(ct)
            ?? throw new ForbiddenException("An active pair is required to send a heartbeat.");

        var partner = await db.PairMembers.AsNoTracking()
            .Where(x => x.PairId == membership.PairId && x.UserId != request.AuthenticatedUserId && x.IsActive)
            .Select(x => new { x.UserId, x.User.DisplayName })
            .SingleOrDefaultAsync(ct)
            ?? throw new ConflictException("The paired partner is unavailable.");

        if (!presence.IsOnline(partner.UserId))
        {
            throw new ConflictException("Partner is not connected right now.");
        }

        var senderName = await db.Users.AsNoTracking()
            .Where(x => x.Id == request.AuthenticatedUserId)
            .Select(x => x.DisplayName)
            .SingleAsync(ct);
        var now = clock.UtcNow;
        var expiresAt = now.Add(policy.EventTtl);
        var envelope = new HeartbeatEnvelope(
            request.EventId,
            membership.PairId,
            request.AuthenticatedUserId,
            partner.UserId,
            expiresAt);
        var reservation = await store.TryReserveAsync(envelope, policy, ct);
        if (reservation == HeartbeatReservationState.RateLimited)
        {
            throw new RateLimitException("Heartbeat send limit reached. Please wait a moment.");
        }
        if (reservation == HeartbeatReservationState.Duplicate)
        {
            return new SendHeartbeatResult(request.EventId, true, true, expiresAt);
        }

        var pattern = new HeartbeatPatternDto(
            request.EventId,
            request.AuthenticatedUserId,
            senderName,
            request.Pattern,
            request.Pattern.Sum(),
            now,
            expiresAt);
        await publisher.PublishPatternAsync(partner.UserId, pattern, ct);
        return new SendHeartbeatResult(request.EventId, true, false, expiresAt);
    }
}
