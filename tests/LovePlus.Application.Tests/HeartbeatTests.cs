using FluentValidation.TestHelper;
using LovePlus.Application.Common.Exceptions;
using LovePlus.Application.Heartbeat;
using LovePlus.Application.Heartbeat.Commands;
using LovePlus.Domain.Pairing;
using LovePlus.Infrastructure.Realtime;

namespace LovePlus.Application.Tests;

public sealed class HeartbeatTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 12, 12, 0, 0, TimeSpan.Zero);
    private static readonly HeartbeatPolicy Policy = new(
        TimeSpan.FromSeconds(20), TimeSpan.FromSeconds(30), 3, 32, 8_000, 1_500);

    [Fact]
    public void Validator_rejects_oversized_and_malformed_patterns()
    {
        var validator = new SendHeartbeatCommandValidator(Policy);
        validator.TestValidate(new SendHeartbeatCommand(Guid.NewGuid(), Guid.NewGuid(), [50, 50]))
            .ShouldHaveValidationErrorFor(x => x.Pattern);
        validator.TestValidate(new SendHeartbeatCommand(Guid.NewGuid(), Guid.NewGuid(), [0, 1_501]))
            .ShouldHaveValidationErrorFor(x => x.Pattern);
        validator.TestValidate(new SendHeartbeatCommand(Guid.NewGuid(), Guid.NewGuid(), [0, -1]))
            .ShouldHaveValidationErrorFor(x => x.Pattern);
        validator.TestValidate(new SendHeartbeatCommand(Guid.NewGuid(), Guid.NewGuid(), [0, 1_500, 1_500, 1_500, 1_500, 1_500, 1_500]))
            .ShouldHaveValidationErrorFor(x => x.Pattern);
    }

    [Fact]
    public async Task Send_targets_only_the_authenticated_users_active_partner()
    {
        var fixture = await Fixture.Create(online: true);

        var result = await fixture.Send.Handle(
            new SendHeartbeatCommand(fixture.SenderId, Guid.NewGuid(), [0, 90, 120, 90]), default);

        Assert.True(result.Accepted);
        Assert.Single(fixture.Publisher.Patterns);
        Assert.Equal(fixture.ReceiverId, fixture.Publisher.Patterns[0].ReceiverUserId);
        Assert.Equal(fixture.SenderId, fixture.Publisher.Patterns[0].Pattern.SenderUserId);
    }

    [Fact]
    public async Task Offline_partner_is_rejected_without_reserving_or_publishing()
    {
        var fixture = await Fixture.Create(online: false);

        await Assert.ThrowsAsync<ConflictException>(() => fixture.Send.Handle(
            new SendHeartbeatCommand(fixture.SenderId, Guid.NewGuid(), [0, 90]), default));
        Assert.Empty(fixture.Publisher.Patterns);
    }

    [Fact]
    public async Task Duplicate_event_is_idempotent_and_published_once()
    {
        var fixture = await Fixture.Create(online: true);
        var eventId = Guid.NewGuid();
        var request = new SendHeartbeatCommand(fixture.SenderId, eventId, [0, 90]);

        await fixture.Send.Handle(request, default);
        var duplicate = await fixture.Send.Handle(request, default);

        Assert.True(duplicate.Duplicate);
        Assert.Single(fixture.Publisher.Patterns);
    }

    [Fact]
    public async Task Application_rate_limit_is_enforced_independent_of_http_limit()
    {
        var fixture = await Fixture.Create(online: true);
        for (var i = 0; i < Policy.MaxEventsPerWindow; i++)
        {
            await fixture.Send.Handle(
                new SendHeartbeatCommand(fixture.SenderId, Guid.NewGuid(), [0, 90]), default);
        }

        await Assert.ThrowsAsync<RateLimitException>(() => fixture.Send.Handle(
            new SendHeartbeatCommand(fixture.SenderId, Guid.NewGuid(), [0, 90]), default));
    }

    [Fact]
    public async Task Receiver_ack_is_forwarded_once_to_sender()
    {
        var fixture = await Fixture.Create(online: true);
        var eventId = Guid.NewGuid();
        await fixture.Send.Handle(
            new SendHeartbeatCommand(fixture.SenderId, eventId, [0, 90]), default);

        var first = await fixture.Acknowledge.Handle(
            new AcknowledgeHeartbeatCommand(fixture.ReceiverId, eventId, HeartbeatAcknowledgementState.Played), default);
        var duplicate = await fixture.Acknowledge.Handle(
            new AcknowledgeHeartbeatCommand(fixture.ReceiverId, eventId, HeartbeatAcknowledgementState.Played), default);

        Assert.True(first.Accepted);
        Assert.True(duplicate.Duplicate);
        Assert.Single(fixture.Publisher.Acknowledgements);
        Assert.Equal(fixture.SenderId, fixture.Publisher.Acknowledgements[0].SenderUserId);
    }

    [Fact]
    public async Task Another_user_cannot_ack_a_pairs_event()
    {
        var fixture = await Fixture.Create(online: true);
        var eventId = Guid.NewGuid();
        await fixture.Send.Handle(
            new SendHeartbeatCommand(fixture.SenderId, eventId, [0, 90]), default);

        await Assert.ThrowsAsync<ForbiddenException>(() => fixture.Acknowledge.Handle(
            new AcknowledgeHeartbeatCommand(Guid.NewGuid(), eventId, HeartbeatAcknowledgementState.Played), default));
    }

    [Fact]
    public async Task Expired_event_cannot_be_acknowledged()
    {
        var fixture = await Fixture.Create(online: true);
        var eventId = Guid.NewGuid();
        await fixture.Send.Handle(
            new SendHeartbeatCommand(fixture.SenderId, eventId, [0, 90]), default);
        fixture.Clock.UtcNow = Now.Add(Policy.EventTtl).AddMilliseconds(1);

        await Assert.ThrowsAsync<NotFoundException>(() => fixture.Acknowledge.Handle(
            new AcknowledgeHeartbeatCommand(fixture.ReceiverId, eventId, HeartbeatAcknowledgementState.Played), default));
    }

    [Fact]
    public async Task Presence_tracks_multiple_devices_until_the_last_disconnects()
    {
        var presence = new InMemoryRealtimePresenceTracker();
        var userId = Guid.NewGuid();
        await presence.ConnectedAsync(userId, "phone", default);
        await presence.ConnectedAsync(userId, "tablet", default);
        await presence.DisconnectedAsync(userId, "phone", default);
        Assert.True(await presence.IsOnlineAsync(userId, default));
        await presence.DisconnectedAsync(userId, "tablet", default);
        Assert.False(await presence.IsOnlineAsync(userId, default));
    }

    [Fact]
    public async Task Pair_rate_limit_is_shared_by_both_directions()
    {
        var store = new InMemoryHeartbeatStore();
        var pairId = Guid.NewGuid();
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();
        for (var index = 0; index < Policy.MaxEventsPerWindow; index++)
        {
            var sender = index % 2 == 0 ? first : second;
            var receiver = sender == first ? second : first;
            var state = await store.TryReserveAsync(
                new HeartbeatEnvelope(Guid.NewGuid(), pairId, sender, receiver, Now.Add(Policy.EventTtl)),
                Policy,
                default);
            Assert.Equal(HeartbeatReservationState.Accepted, state);
        }

        var blocked = await store.TryReserveAsync(
            new HeartbeatEnvelope(Guid.NewGuid(), pairId, second, first, Now.Add(Policy.EventTtl)),
            Policy,
            default);
        Assert.Equal(HeartbeatReservationState.RateLimited, blocked);
    }

    private sealed record Fixture(
        Guid SenderId,
        Guid ReceiverId,
        TestClock Clock,
        TestHeartbeatPublisher Publisher,
        SendHeartbeatCommandHandler Send,
        AcknowledgeHeartbeatCommandHandler Acknowledge)
    {
        public static async Task<Fixture> Create(bool online)
        {
            var db = TestDb.Create();
            var users = PairingTests.AddUsers(db, 2);
            var pair = new CouplePair { CreatedAtUtc = Now };
            db.CouplePairs.Add(pair);
            db.PairMembers.AddRange(
                new PairMember { PairId = pair.Id, UserId = users[0].Id, JoinedAtUtc = Now },
                new PairMember { PairId = pair.Id, UserId = users[1].Id, JoinedAtUtc = Now });
            await db.SaveChangesAsync();

            var store = new InMemoryHeartbeatStore();
            var presence = new InMemoryRealtimePresenceTracker();
            if (online)
            {
                await presence.ConnectedAsync(users[1].Id, "partner-phone", default);
            }
            var publisher = new TestHeartbeatPublisher();
            var clock = new TestClock(Now);
            return new Fixture(
                users[0].Id,
                users[1].Id,
                clock,
                publisher,
                new SendHeartbeatCommandHandler(db, store, presence, publisher, Policy, clock),
                new AcknowledgeHeartbeatCommandHandler(store, publisher, clock));
        }
    }

    private sealed class TestHeartbeatPublisher : IHeartbeatPublisher
    {
        public List<(Guid ReceiverUserId, HeartbeatPatternDto Pattern)> Patterns { get; } = [];
        public List<(Guid SenderUserId, HeartbeatAcknowledgementDto Acknowledgement)> Acknowledgements { get; } = [];

        public Task PublishPatternAsync(Guid receiverUserId, HeartbeatPatternDto pattern, CancellationToken cancellationToken)
        {
            Patterns.Add((receiverUserId, pattern));
            return Task.CompletedTask;
        }

        public Task PublishAcknowledgementAsync(
            Guid senderUserId,
            HeartbeatAcknowledgementDto acknowledgement,
            CancellationToken cancellationToken)
        {
            Acknowledgements.Add((senderUserId, acknowledgement));
            return Task.CompletedTask;
        }
    }
}
