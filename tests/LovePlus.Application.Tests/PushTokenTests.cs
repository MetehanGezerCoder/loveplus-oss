using LovePlus.Application.Identity.Commands;
using LovePlus.Domain.Identity;
using Microsoft.EntityFrameworkCore;

namespace LovePlus.Application.Tests;

public sealed class PushTokenTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 15, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Registration_binds_the_token_to_the_authenticated_session()
    {
        await using var db = TestDb.Create();
        var (user, session) = await SeedAsync(db, "metehan@example.test");
        var handler = new RegisterPushTokenCommandHandler(db, new TestClock(Now));

        var result = await handler.Handle(
            new RegisterPushTokenCommand(user.Id, session.Id, "android", new string('a', 32)),
            CancellationToken.None);

        var stored = await db.DevicePushTokens.SingleAsync();
        Assert.True(result.Accepted);
        Assert.Equal(user.Id, stored.UserId);
        Assert.Equal(session.Id, stored.DeviceSessionId);
        Assert.Null(stored.DisabledAtUtc);
    }

    [Fact]
    public async Task Re_registering_the_same_session_replaces_the_token_instead_of_duplicating_it()
    {
        await using var db = TestDb.Create();
        var (user, session) = await SeedAsync(db, "metehan@example.test");
        var handler = new RegisterPushTokenCommandHandler(db, new TestClock(Now));

        await handler.Handle(
            new RegisterPushTokenCommand(user.Id, session.Id, "android", new string('a', 32)),
            CancellationToken.None);
        await handler.Handle(
            new RegisterPushTokenCommand(user.Id, session.Id, "android", new string('b', 32)),
            CancellationToken.None);

        var stored = await db.DevicePushTokens.SingleAsync();
        Assert.Equal(new string('b', 32), stored.Token);
    }

    [Fact]
    public async Task A_token_that_moves_to_another_account_is_retired_on_the_previous_owner()
    {
        await using var db = TestDb.Create();
        var (metehan, metehanSession) = await SeedAsync(db, "metehan@example.test");
        var (meliha, melihaSession) = await SeedAsync(db, "meliha@example.test");
        var handler = new RegisterPushTokenCommandHandler(db, new TestClock(Now));
        var token = new string('c', 32);

        await handler.Handle(
            new RegisterPushTokenCommand(metehan.Id, metehanSession.Id, "android", token),
            CancellationToken.None);
        await handler.Handle(
            new RegisterPushTokenCommand(meliha.Id, melihaSession.Id, "android", token),
            CancellationToken.None);

        var active = await db.DevicePushTokens.Where(x => x.DisabledAtUtc == null).ToListAsync();
        var retired = await db.DevicePushTokens.Where(x => x.DisabledAtUtc != null).ToListAsync();
        Assert.Equal(meliha.Id, Assert.Single(active).UserId);
        Assert.Equal(token, active[0].Token);
        Assert.DoesNotContain(retired, x => x.Token == token);
    }

    [Theory]
    [InlineData("ios", "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")]
    [InlineData("android", "short")]
    [InlineData("android", "")]
    public void Unsupported_platforms_and_implausible_tokens_are_rejected(string platform, string token)
    {
        var validator = new RegisterPushTokenCommandValidator();

        var result = validator.Validate(
            new RegisterPushTokenCommand(Guid.NewGuid(), Guid.NewGuid(), platform, token));

        Assert.False(result.IsValid);
    }

    private static async Task<(User User, DeviceSession Session)> SeedAsync(
        Infrastructure.Persistence.LovePlusDbContext db,
        string email)
    {
        var user = new User
        {
            Email = email,
            NormalizedEmail = email.ToUpperInvariant(),
            DisplayName = email.Split('@')[0],
            PasswordHash = "hashed:test",
            CreatedAtUtc = Now
        };
        var session = new DeviceSession
        {
            UserId = user.Id,
            DeviceId = $"device-{Guid.NewGuid():N}",
            DeviceName = "Test phone",
            CreatedAtUtc = Now,
            LastSeenAtUtc = Now
        };
        db.Users.Add(user);
        db.DeviceSessions.Add(session);
        await db.SaveChangesAsync(CancellationToken.None);
        return (user, session);
    }
}
