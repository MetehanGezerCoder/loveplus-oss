using FluentValidation.TestHelper;
using LovePlus.Application.Common.Exceptions;
using LovePlus.Application.Realtime;
using LovePlus.Application.Realtime.Commands;
using LovePlus.Domain.Pairing;
using LovePlus.Domain.Realtime;
using Microsoft.EntityFrameworkCore;

namespace LovePlus.Application.Tests;

public sealed class RealtimeStatusTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 11, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Status_validator_rejects_invalid_location_battery_and_sequence()
    {
        var validator = new UpdateUserStatusCommandValidator();
        var result = validator.TestValidate(new UpdateUserStatusCommand(
            Guid.NewGuid(), "device", new LocationInput(91, -181, -1, -1, 361, 25_000, Now),
            true, true, 101, false, BatteryState.Discharging,
            ActivityType.Walking, MoodType.None, Now, 0, TelemetrySource.RealDevice));

        result.ShouldHaveValidationErrorFor("Location.Latitude");
        result.ShouldHaveValidationErrorFor("Location.Longitude");
        result.ShouldHaveValidationErrorFor("Location.Accuracy");
        result.ShouldHaveValidationErrorFor(x => x.BatteryLevel);
        result.ShouldHaveValidationErrorFor(x => x.SequenceNumber);
    }

    [Fact]
    public void Location_is_rejected_when_sharing_is_off()
    {
        var validator = new UpdateUserStatusCommandValidator();
        var result = validator.TestValidate(Status(Guid.NewGuid(), 1, 50) with { IsLocationSharingEnabled = false });

        result.ShouldHaveValidationErrorFor(x => x.Location);
    }

    [Fact]
    public void Mood_validator_accepts_phase_three_values_and_rejects_unknown_values()
    {
        var validator = new UpdateUserStatusCommandValidator();
        validator.TestValidate(Status(Guid.NewGuid(), 1, 50) with { Mood = MoodType.InLove })
            .ShouldNotHaveValidationErrorFor(x => x.Mood);
        validator.TestValidate(Status(Guid.NewGuid(), 1, 50) with { Mood = MoodType.MissingYou })
            .ShouldNotHaveValidationErrorFor(x => x.Mood);
        validator.TestValidate(Status(Guid.NewGuid(), 1, 50) with { Mood = MoodType.Sulky })
            .ShouldNotHaveValidationErrorFor(x => x.Mood);
        validator.TestValidate(Status(Guid.NewGuid(), 1, 50) with { Mood = (MoodType)999 })
            .ShouldHaveValidationErrorFor(x => x.Mood);
    }

    [Fact]
    public void Distance_calculation_matches_known_Istanbul_segment()
    {
        var distance = new GeoDistanceCalculator().CalculateMeters(
            41.0082, 28.9784, 41.0430, 29.0094);

        Assert.InRange(distance, 4_500, 4_800);
    }

    [Fact]
    public async Task Critical_battery_event_is_emitted_once_until_battery_recovers_above_five()
    {
        var fixture = await Fixture.Create();
        await fixture.Handler.Handle(Status(fixture.UserId, 1, 5), default);
        await fixture.Handler.Handle(Status(fixture.UserId, 2, 4), default);
        await fixture.Handler.Handle(Status(fixture.UserId, 3, 6), default);
        await fixture.Handler.Handle(Status(fixture.UserId, 4, 5), default);

        Assert.Equal(2, await fixture.Db.CriticalBatteryEvents.CountAsync());
        Assert.Equal(2, fixture.Publisher.CriticalEvents.Count);
        Assert.Equal(2, fixture.Push.Count);
    }

    [Fact]
    public async Task Stale_sequence_is_rejected_and_cannot_overwrite_new_location()
    {
        var fixture = await Fixture.Create();
        await fixture.Handler.Handle(Status(fixture.UserId, 2, 50), default);

        await Assert.ThrowsAsync<ConflictException>(() =>
            fixture.Handler.Handle(Status(fixture.UserId, 1, 49), default));
        var stored = await fixture.Store.GetAsync(fixture.UserId, default);
        Assert.Equal(2, stored!.SequenceNumber);
    }

    [Fact]
    public async Task New_device_can_start_its_own_sequence_but_older_device_packet_cannot_take_over()
    {
        var fixture = await Fixture.Create();
        await fixture.Handler.Handle(Status(fixture.UserId, 8, 50), default);

        await fixture.Handler.Handle(Status(fixture.UserId, 1, 60) with
        {
            AuthenticatedDeviceId = "device-2",
            RecordedAtUtc = Now.AddSeconds(1),
            Location = new LocationInput(41.01, 28.98, 8, null, null, null, Now.AddSeconds(1))
        }, default);

        await Assert.ThrowsAsync<ConflictException>(() => fixture.Handler.Handle(
            Status(fixture.UserId, 9, 40) with { RecordedAtUtc = Now }, default));

        var stored = await fixture.Store.GetAsync(fixture.UserId, default);
        Assert.Equal("device-2", stored!.DeviceId);
        Assert.Equal(1, stored.SequenceNumber);
        Assert.Equal(60, stored.BatteryLevel);
    }

    [Fact]
    public async Task Simulator_cannot_replace_real_device_telemetry()
    {
        var fixture = await Fixture.Create(simulatorEnabled: true);
        await fixture.Handler.Handle(Status(fixture.UserId, 1, 50), default);

        await Assert.ThrowsAsync<ConflictException>(() => fixture.Handler.Handle(
            Status(fixture.UserId, 2, 50) with { Source = TelemetrySource.Simulator }, default));
    }

    [Fact]
    public void Presence_policy_has_online_recent_and_offline_states()
    {
        var policy = TestPolicies.Realtime();
        Assert.Equal(PresenceState.Online, policy.Presence(Now.AddMinutes(-1), Now));
        Assert.Equal(PresenceState.RecentlyOnline, policy.Presence(Now.AddMinutes(-5), Now));
        Assert.Equal(PresenceState.Offline, policy.Presence(Now.AddMinutes(-11), Now));
    }

    [Theory]
    [InlineData(99, 0, 0, ProximityLevel.SamePlace)]
    [InlineData(100, 0, 0, ProximityLevel.SamePlace)]
    [InlineData(101, 0, 0, ProximityLevel.VeryClose)]
    [InlineData(500, 0, 0, ProximityLevel.VeryClose)]
    [InlineData(501, 0, 0, ProximityLevel.Nearby)]
    [InlineData(2000, 0, 0, ProximityLevel.Nearby)]
    [InlineData(2001, 0, 0, ProximityLevel.SameArea)]
    [InlineData(10001, 0, 0, ProximityLevel.Far)]
    [InlineData(60, 25, 25, ProximityLevel.VeryClose)]
    public void Proximity_policy_respects_boundaries_and_accuracy(
        double distance,
        double subjectAccuracy,
        double counterpartAccuracy,
        ProximityLevel expected)
    {
        Assert.Equal(expected, TestPolicies.Realtime().Proximity(distance, subjectAccuracy, counterpartAccuracy));
    }

    [Fact]
    public void Mapper_hides_mood_and_previous_coordinates_when_partner_privacy_is_off()
    {
        var subject = new UserLiveStatus(
            Guid.NewGuid(), Guid.NewGuid(),
            new LocationSample(41, 29, 10, null, null, null, Now),
            false, true, 50, false, BatteryState.Discharging, ActivityType.Stationary,
            MoodType.InLove, Now, "partner-device", 1, TelemetrySource.RealDevice,
            IsMoodSharingEnabled: false);
        var own = subject with
        {
            UserId = Guid.NewGuid(),
            Location = new LocationSample(41.001, 29.001, 10, null, null, null, Now),
            IsLocationSharingEnabled = true,
            IsMoodSharingEnabled = true
        };

        var mapped = PartnerStatusMapper.Map(
            subject, "Partner", own, TestPolicies.Realtime(), new GeoDistanceCalculator(), Now);

        Assert.Equal(MoodType.None, mapped.Mood);
        Assert.False(mapped.IsMoodSharingEnabled);
        Assert.Null(mapped.Location);
        Assert.Null(mapped.DistanceMeters);
        Assert.Equal(ProximityLevel.Unavailable, mapped.ProximityLevel);
        Assert.Equal(DistanceAvailability.PartnerSharingOff, mapped.DistanceAvailability);
    }

    private static UpdateUserStatusCommand Status(Guid userId, long sequence, int battery) => new(
        userId,
        "device-1",
        new LocationInput(41.0082, 28.9784, 10, 1.2, 90, 30, Now),
        true,
        true,
        battery,
        false,
        BatteryState.Discharging,
        ActivityType.Stationary,
        MoodType.Calm,
        Now,
        sequence,
        TelemetrySource.RealDevice);

    private sealed record Fixture(
        LovePlus.Infrastructure.Persistence.LovePlusDbContext Db,
        Guid UserId,
        TestLiveStatusStore Store,
        TestPublisher Publisher,
        TestPushSender Push,
        UpdateUserStatusCommandHandler Handler)
    {
        public static async Task<Fixture> Create(bool simulatorEnabled = false)
        {
            var db = TestDb.Create();
            var users = PairingTests.AddUsers(db, 2);
            var pair = new CouplePair { CreatedAtUtc = Now };
            db.CouplePairs.Add(pair);
            db.PairMembers.AddRange(
                new PairMember { PairId = pair.Id, UserId = users[0].Id, JoinedAtUtc = Now },
                new PairMember { PairId = pair.Id, UserId = users[1].Id, JoinedAtUtc = Now });
            await db.SaveChangesAsync();
            var store = new TestLiveStatusStore();
            var publisher = new TestPublisher();
            var push = new TestPushSender();
            var handler = new UpdateUserStatusCommandHandler(
                db,
                store,
                publisher,
                push,
                new GeoDistanceCalculator(),
                TestPolicies.Realtime(simulatorEnabled),
                new TestClock(Now));
            return new Fixture(db, users[0].Id, store, publisher, push, handler);
        }
    }
}
