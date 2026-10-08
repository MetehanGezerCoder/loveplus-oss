using LovePlus.Domain.Common;
using LovePlus.Domain.Identity;
using LovePlus.Domain.Pairing;
using LovePlus.Domain.Realtime;
using Microsoft.EntityFrameworkCore;

namespace LovePlus.Application.Common.Abstractions;

public interface IApplicationDbContext
{
    DbSet<User> Users { get; }
    DbSet<DeviceSession> DeviceSessions { get; }
    DbSet<DevicePushToken> DevicePushTokens { get; }
    DbSet<RefreshToken> RefreshTokens { get; }
    DbSet<CouplePair> CouplePairs { get; }
    DbSet<PairMember> PairMembers { get; }
    DbSet<PairingCode> PairingCodes { get; }
    DbSet<UserLiveStatusSnapshot> UserLiveStatusSnapshots { get; }
    DbSet<CriticalBatteryEvent> CriticalBatteryEvents { get; }
    DbSet<OutboxMessage> OutboxMessages { get; }
    Task<int> SaveChangesAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Runs <paramref name="operation"/> inside a transaction at the given isolation level, via
    /// the context's execution strategy. Production enables Npgsql's retry-on-failure, and EF
    /// Core refuses a manually opened <c>BeginTransactionAsync</c> under a retrying strategy
    /// (it cannot safely replay a user transaction) — this is the one supported way to combine
    /// the two. Do not call <c>Database.BeginTransactionAsync</c> directly.
    /// </summary>
    Task<TResult> ExecuteInTransactionAsync<TResult>(
        System.Data.IsolationLevel isolationLevel,
        Func<CancellationToken, Task<TResult>> operation,
        CancellationToken cancellationToken);
}

public interface IClock
{
    DateTimeOffset UtcNow { get; }
}

public interface IPasswordService
{
    string Hash(User user, string password);
    bool Verify(User user, string hash, string password);
}

public interface ITokenService
{
    string CreateAccessToken(User user, DeviceSession session, DateTimeOffset now);
    string CreateTelemetryToken(User user, DeviceSession session, DateTimeOffset now);
    string GenerateRefreshToken();
    string HashSecret(string secret);
    string GeneratePairingCode();
    TimeSpan AccessTokenLifetime { get; }
    TimeSpan TelemetryTokenLifetime { get; }
    TimeSpan RefreshTokenLifetime { get; }
}
