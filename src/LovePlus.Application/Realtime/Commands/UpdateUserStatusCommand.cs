using System.Text.Json;
using FluentValidation;
using LovePlus.Application.Common.Abstractions;
using LovePlus.Application.Common.Exceptions;
using LovePlus.Domain.Common;
using LovePlus.Domain.Realtime;
using MediatR;
using Microsoft.EntityFrameworkCore;
using NetTopologySuite.Geometries;

namespace LovePlus.Application.Realtime.Commands;

public sealed record LocationInput(
    double Latitude,
    double Longitude,
    double Accuracy,
    double? Speed,
    double? Heading,
    double? Altitude,
    DateTimeOffset RecordedAtUtc);

public sealed record UpdateUserStatusCommand(
    Guid AuthenticatedUserId,
    string AuthenticatedDeviceId,
    LocationInput? Location,
    bool IsLocationSharingEnabled,
    bool ShareLastKnownLocation,
    int BatteryLevel,
    bool IsCharging,
    BatteryState BatteryState,
    ActivityType ActivityType,
    MoodType Mood,
    DateTimeOffset RecordedAtUtc,
    long SequenceNumber,
    TelemetrySource Source,
    bool IsMoodSharingEnabled = true) : IRequest<UserStatusResult>;

public sealed class UpdateUserStatusCommandValidator : AbstractValidator<UpdateUserStatusCommand>
{
    public UpdateUserStatusCommandValidator()
    {
        RuleFor(x => x.AuthenticatedUserId).NotEmpty();
        RuleFor(x => x.AuthenticatedDeviceId).NotEmpty().MaximumLength(128);
        RuleFor(x => x.BatteryLevel).InclusiveBetween(0, 100);
        RuleFor(x => x.BatteryState).IsInEnum();
        RuleFor(x => x.ActivityType).IsInEnum();
        RuleFor(x => x.Mood).IsInEnum();
        RuleFor(x => x.Source).IsInEnum();
        RuleFor(x => x.SequenceNumber).GreaterThan(0);
        When(x => x.Location is not null, () =>
        {
            RuleFor(x => x.Location!.Latitude).InclusiveBetween(-90, 90);
            RuleFor(x => x.Location!.Longitude).InclusiveBetween(-180, 180);
            RuleFor(x => x.Location!.Accuracy).GreaterThanOrEqualTo(0).LessThanOrEqualTo(10_000);
            RuleFor(x => x.Location!.Speed).GreaterThanOrEqualTo(0).When(x => x.Location!.Speed is not null);
            RuleFor(x => x.Location!.Heading).InclusiveBetween(0, 360).When(x => x.Location!.Heading is not null);
            RuleFor(x => x.Location!.Altitude).InclusiveBetween(-500, 20_000).When(x => x.Location!.Altitude is not null);
        });
        RuleFor(x => x.Location).Null().When(x => !x.IsLocationSharingEnabled);
    }
}

public sealed class UpdateUserStatusCommandHandler(
    IApplicationDbContext db,
    ILiveStatusStore store,
    IPartnerStatusPublisher publisher,
    IPushNotificationSender pushNotifications,
    IGeoDistanceCalculator distances,
    RealtimeStatusPolicy policy,
    IClock clock) : IRequestHandler<UpdateUserStatusCommand, UserStatusResult>
{
    private static readonly TimeSpan SnapshotInterval = TimeSpan.FromMinutes(1);

    public async Task<UserStatusResult> Handle(UpdateUserStatusCommand request, CancellationToken ct)
    {
        var now = clock.UtcNow;
        if (request.RecordedAtUtc < now.Subtract(policy.MaxOfflinePacketAge) || request.RecordedAtUtc > now.AddMinutes(2))
        {
            throw new ConflictException("Status timestamp is outside the accepted freshness window.");
        }
        if (request.Source == TelemetrySource.Simulator && !policy.SimulatorEnabled)
        {
            throw new ForbiddenException("Simulator telemetry is disabled.");
        }

        var membership = await db.PairMembers.AsNoTracking()
            .SingleOrDefaultAsync(x => x.UserId == request.AuthenticatedUserId && x.IsActive, ct)
            ?? throw new ForbiddenException("An active pair is required to publish status.");
        var ownDisplayName = await db.Users.AsNoTracking()
            .Where(x => x.Id == request.AuthenticatedUserId)
            .Select(x => x.DisplayName)
            .SingleAsync(ct);
        var partner = await db.PairMembers.AsNoTracking()
            .Where(x => x.PairId == membership.PairId && x.UserId != request.AuthenticatedUserId && x.IsActive)
            .Select(x => new { x.UserId })
            .SingleAsync(ct);

        var previous = await store.GetAsync(request.AuthenticatedUserId, ct);
        if (previous is not null
            && previous.DeviceId == request.AuthenticatedDeviceId
            && request.SequenceNumber <= previous.SequenceNumber)
        {
            throw new ConflictException("Status sequence number is stale or duplicated.");
        }
        if (previous is not null
            && previous.DeviceId != request.AuthenticatedDeviceId
            && request.RecordedAtUtc <= previous.RecordedAtUtc)
        {
            throw new ConflictException("A newer device status is already active for this user.");
        }
        if (previous is not null && previous.Source == TelemetrySource.RealDevice && request.Source == TelemetrySource.Simulator)
        {
            throw new ConflictException("Real-device telemetry is active for this user.");
        }

        var location = request.IsLocationSharingEnabled && request.Location is not null
            ? new LocationSample(
                request.Location.Latitude,
                request.Location.Longitude,
                request.Location.Accuracy,
                request.Location.Speed,
                request.Location.Heading,
                request.Location.Altitude,
                request.Location.RecordedAtUtc)
            : request.ShareLastKnownLocation && previous?.Source == request.Source
                ? previous.Location
                : null;
        var status = new UserLiveStatus(
            request.AuthenticatedUserId,
            membership.PairId,
            location,
            request.IsLocationSharingEnabled,
            request.ShareLastKnownLocation,
            request.BatteryLevel,
            request.IsCharging,
            request.BatteryState,
            request.ActivityType,
            request.Mood,
            request.RecordedAtUtc,
            request.AuthenticatedDeviceId,
            request.SequenceNumber,
            request.Source,
            request.IsMoodSharingEnabled);
        if (!await store.TrySetAsync(status, policy.LiveStatusTtl, ct))
        {
            throw new ConflictException("Status sequence number is stale or duplicated.");
        }

        var openCritical = await db.CriticalBatteryEvents
            .SingleOrDefaultAsync(x => x.UserId == status.UserId && x.PairId == status.PairId && x.RecoveredAtUtc == null, ct);
        var criticalCreated = false;
        CriticalBatteryDto? criticalDto = null;
        if (status.IsCriticalBattery && openCritical is null)
        {
            openCritical = new CriticalBatteryEvent
            {
                UserId = status.UserId,
                PairId = status.PairId,
                BatteryLevel = status.BatteryLevel,
                LastKnownLocation = status.Location is null ? null : CreatePoint(status.Location),
                OccurredAtUtc = now
            };
            db.CriticalBatteryEvents.Add(openCritical);
            criticalCreated = true;
            criticalDto = new CriticalBatteryDto(
                openCritical.Id, status.UserId, status.BatteryLevel, status.Location is not null, now);
            db.OutboxMessages.Add(new OutboxMessage
            {
                Type = "CriticalBatteryDetected",
                Payload = JsonSerializer.Serialize(new
                {
                    EventId = openCritical.Id,
                    status.UserId,
                    status.PairId,
                    status.BatteryLevel,
                    LastKnownLocationAvailable = status.Location is not null
                }),
                OccurredAtUtc = now
            });
        }
        else if (status.BatteryLevel > 5 && openCritical is not null)
        {
            openCritical.RecoveredAtUtc = now;
        }

        var snapshot = await db.UserLiveStatusSnapshots
            .SingleOrDefaultAsync(x => x.UserId == status.UserId, ct);
        if (snapshot is null
            || snapshot.DeviceId != status.DeviceId
            || status.IsCriticalBattery
            || now - snapshot.PersistedAtUtc >= SnapshotInterval
            || location is not null)
        {
            if (snapshot is null)
            {
                snapshot = new UserLiveStatusSnapshot
                {
                    UserId = status.UserId,
                    PairId = status.PairId,
                    DeviceId = status.DeviceId
                };
                db.UserLiveStatusSnapshots.Add(snapshot);
            }
            ApplySnapshot(snapshot, status, now);
        }

        await db.SaveChangesAsync(ct);

        var partnerLive = await store.GetAsync(partner.UserId, ct);
        var dto = PartnerStatusMapper.Map(status, ownDisplayName, partnerLive, policy, distances, now);
        await publisher.PublishStatusAsync(status.PairId, dto, ct);

        if (criticalDto is not null)
        {
            await publisher.PublishCriticalBatteryAsync(status.PairId, criticalDto, ct);
            await pushNotifications.SendCriticalBatteryAsync(partner.UserId, criticalDto, ct);
        }

        return new UserStatusResult(true, criticalCreated, now);
    }

    private static Point CreatePoint(LocationSample location) =>
        new(location.Longitude, location.Latitude) { SRID = 4326 };

    private static void ApplySnapshot(UserLiveStatusSnapshot snapshot, UserLiveStatus status, DateTimeOffset now)
    {
        snapshot.PairId = status.PairId;
        if (status.Location is not null)
        {
            snapshot.Location = CreatePoint(status.Location);
            snapshot.Accuracy = status.Location.Accuracy;
            snapshot.Speed = status.Location.Speed;
            snapshot.Heading = status.Location.Heading;
            snapshot.Altitude = status.Location.Altitude;
            snapshot.LocationRecordedAtUtc = status.Location.RecordedAtUtc;
        }
        else if (!status.ShareLastKnownLocation)
        {
            snapshot.Location = null;
            snapshot.Accuracy = null;
            snapshot.Speed = null;
            snapshot.Heading = null;
            snapshot.Altitude = null;
            snapshot.LocationRecordedAtUtc = null;
        }
        snapshot.IsLocationSharingEnabled = status.IsLocationSharingEnabled;
        snapshot.ShareLastKnownLocation = status.ShareLastKnownLocation;
        snapshot.BatteryLevel = status.BatteryLevel;
        snapshot.IsCharging = status.IsCharging;
        snapshot.BatteryState = status.BatteryState;
        snapshot.ActivityType = status.ActivityType;
        snapshot.Mood = status.Mood;
        snapshot.IsMoodSharingEnabled = status.IsMoodSharingEnabled;
        snapshot.RecordedAtUtc = status.RecordedAtUtc;
        snapshot.PersistedAtUtc = now;
        snapshot.DeviceId = status.DeviceId;
        snapshot.SequenceNumber = status.SequenceNumber;
        snapshot.IsCriticalBatteryLocation = status.IsCriticalBattery && status.Location is not null;
        snapshot.Source = status.Source;
    }
}
