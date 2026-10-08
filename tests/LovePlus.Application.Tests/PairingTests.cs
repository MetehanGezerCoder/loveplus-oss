using LovePlus.Application.Common.Exceptions;
using LovePlus.Application.Pairing.Commands;
using LovePlus.Application.Pairing.Queries;
using LovePlus.Application.Realtime.Queries;
using LovePlus.Domain.Identity;
using LovePlus.Domain.Pairing;
using Microsoft.EntityFrameworkCore;

namespace LovePlus.Application.Tests;

public sealed class PairingTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 11, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Expired_pairing_code_is_rejected()
    {
        await using var db = TestDb.Create();
        var users = AddUsers(db, 2);
        var tokens = new TestTokenService();
        db.PairingCodes.Add(new PairingCode
        {
            CreatorUserId = users[0].Id,
            CodeHash = tokens.HashSecret("EXPIRED1"),
            CreatedAtUtc = Now.AddMinutes(-20),
            ExpiresAtUtc = Now.AddMinutes(-10)
        });
        await db.SaveChangesAsync();

        var handler = new RedeemPairingCodeCommandHandler(db, tokens, new TestClock(Now));
        await Assert.ThrowsAsync<NotFoundException>(() =>
            handler.Handle(new RedeemPairingCodeCommand(users[1].Id, "EXPIRED1"), default));
    }

    [Fact]
    public async Task Pairing_code_is_single_use()
    {
        await using var db = TestDb.Create();
        var users = AddUsers(db, 3);
        var tokens = new TestTokenService();
        db.PairingCodes.Add(new PairingCode
        {
            CreatorUserId = users[0].Id,
            CodeHash = tokens.HashSecret("ONEUSE01"),
            CreatedAtUtc = Now,
            ExpiresAtUtc = Now.AddMinutes(10)
        });
        await db.SaveChangesAsync();
        var handler = new RedeemPairingCodeCommandHandler(db, tokens, new TestClock(Now));

        await handler.Handle(new RedeemPairingCodeCommand(users[1].Id, "ONEUSE01"), default);
        await Assert.ThrowsAsync<ConflictException>(() =>
            handler.Handle(new RedeemPairingCodeCommand(users[2].Id, "ONEUSE01"), default));
    }

    [Fact]
    public async Task User_without_pair_cannot_read_another_pairs_status()
    {
        await using var db = TestDb.Create();
        var users = AddUsers(db, 3);
        var pair = new CouplePair { CreatedAtUtc = Now };
        db.CouplePairs.Add(pair);
        db.PairMembers.AddRange(
            new PairMember { PairId = pair.Id, UserId = users[0].Id, JoinedAtUtc = Now },
            new PairMember { PairId = pair.Id, UserId = users[1].Id, JoinedAtUtc = Now });
        await db.SaveChangesAsync();
        var handler = new GetPartnerStatusQueryHandler(
            db,
            new TestLiveStatusStore(),
            new LovePlus.Application.Realtime.GeoDistanceCalculator(),
            TestPolicies.Realtime(),
            new TestClock(Now));

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            handler.Handle(new GetPartnerStatusQuery(users[2].Id), default));
    }

    internal static User[] AddUsers(LovePlus.Infrastructure.Persistence.LovePlusDbContext db, int count)
    {
        var users = Enumerable.Range(1, count).Select(i => new User
        {
            Email = $"person{i}@example.com",
            NormalizedEmail = $"PERSON{i}@EXAMPLE.COM",
            DisplayName = $"Person {i}",
            PasswordHash = "hash",
            CreatedAtUtc = Now
        }).ToArray();
        db.Users.AddRange(users);
        return users;
    }
}
