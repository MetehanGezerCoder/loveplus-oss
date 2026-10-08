using LovePlus.Application.Common.Exceptions;
using LovePlus.Application.Identity.Commands;
using LovePlus.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;

namespace LovePlus.Application.Tests;

public sealed class AuthenticationTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 11, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Register_then_login_returns_device_sessions()
    {
        await using var db = TestDb.Create();
        var clock = new TestClock(Now);
        var tokens = new TestTokenService();
        var passwords = new PasswordService();
        var registered = await new RegisterCommandHandler(db, passwords, tokens, clock).Handle(
            new RegisterCommand("person@example.com", "correct-horse", "Person", "device-1", "Pixel"), default);
        var loggedIn = await new LoginCommandHandler(db, passwords, tokens, clock).Handle(
            new LoginCommand("person@example.com", "correct-horse", "device-2", "Tablet"), default);

        Assert.Equal(registered.User.Id, loggedIn.User.Id);
        Assert.Equal(2, await db.DeviceSessions.CountAsync());
        Assert.DoesNotContain("correct-horse", (await db.Users.SingleAsync()).PasswordHash, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Refresh_rotates_secret_and_reuse_revokes_session()
    {
        await using var db = TestDb.Create();
        var clock = new TestClock(Now);
        var tokens = new TestTokenService();
        var passwords = new PasswordService();
        var initial = await new RegisterCommandHandler(db, passwords, tokens, clock).Handle(
            new RegisterCommand("person@example.com", "correct-horse", "Person", "device-1", "Pixel"), default);
        var handler = new RefreshTokenCommandHandler(db, tokens, clock);
        var rotated = await handler.Handle(new RefreshTokenCommand(initial.RefreshToken, "device-1"), default);

        Assert.NotEqual(initial.RefreshToken, rotated.RefreshToken);
        await Assert.ThrowsAsync<AuthenticationException>(() =>
            handler.Handle(new RefreshTokenCommand(initial.RefreshToken, "device-1"), default));
        Assert.NotNull((await db.DeviceSessions.SingleAsync()).RevokedAtUtc);
    }
}
