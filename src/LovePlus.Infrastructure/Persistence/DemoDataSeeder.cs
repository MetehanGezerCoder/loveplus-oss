using LovePlus.Application.Common.Abstractions;
using LovePlus.Domain.Identity;
using LovePlus.Domain.Pairing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace LovePlus.Infrastructure.Persistence;

public static class DemoDataSeeder
{
    private const string AlexEmail = "alex@example.com";
    private const string TaylorEmail = "taylor@example.com";

    public static async Task SeedAsync(
        IServiceProvider services,
        IConfiguration configuration,
        IHostEnvironment environment,
        CancellationToken cancellationToken)
    {
        if (!environment.IsDevelopment() || !configuration.GetValue("Demo:SeedEnabled", false))
        {
            return;
        }
        var password = configuration["Demo:Password"];
        if (string.IsNullOrWhiteSpace(password) || password.Length < 10)
        {
            throw new InvalidOperationException("Demo__Password must contain at least 10 characters when demo seed is enabled.");
        }

        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<LovePlusDbContext>();
        if (db.Database.IsRelational())
        {
            await db.Database.MigrateAsync(cancellationToken);
        }
        else
        {
            await db.Database.EnsureCreatedAsync(cancellationToken);
        }
        var passwords = scope.ServiceProvider.GetRequiredService<IPasswordService>();
        var clock = scope.ServiceProvider.GetRequiredService<IClock>();
        var alex = await UpsertUser(db, passwords, AlexEmail, "Alex", password, clock.UtcNow, cancellationToken);
        var taylor = await UpsertUser(db, passwords, TaylorEmail, "Taylor", password, clock.UtcNow, cancellationToken);

        var alexMembership = await db.PairMembers.SingleOrDefaultAsync(
            x => x.UserId == alex.Id && x.IsActive, cancellationToken);
        var taylorMembership = await db.PairMembers.SingleOrDefaultAsync(
            x => x.UserId == taylor.Id && x.IsActive, cancellationToken);
        if (alexMembership is null && taylorMembership is null)
        {
            var pair = new CouplePair { CreatedAtUtc = clock.UtcNow };
            db.CouplePairs.Add(pair);
            db.PairMembers.AddRange(
                new PairMember { PairId = pair.Id, UserId = alex.Id, JoinedAtUtc = clock.UtcNow },
                new PairMember { PairId = pair.Id, UserId = taylor.Id, JoinedAtUtc = clock.UtcNow });
        }
        else if (alexMembership?.PairId != taylorMembership?.PairId)
        {
            throw new InvalidOperationException("Demo users already belong to different active pairs.");
        }
        await db.SaveChangesAsync(cancellationToken);
    }

    private static async Task<User> UpsertUser(
        LovePlusDbContext db,
        IPasswordService passwords,
        string email,
        string displayName,
        string password,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var normalized = email.ToUpperInvariant();
        var user = await db.Users.SingleOrDefaultAsync(x => x.NormalizedEmail == normalized, cancellationToken);
        if (user is not null) return user;
        user = new User
        {
            Email = email,
            NormalizedEmail = normalized,
            DisplayName = displayName,
            PasswordHash = string.Empty,
            CreatedAtUtc = now
        };
        user.PasswordHash = passwords.Hash(user, password);
        db.Users.Add(user);
        await db.SaveChangesAsync(cancellationToken);
        return user;
    }
}
