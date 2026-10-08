using System.Data;
using LovePlus.Application.Common.Abstractions;
using LovePlus.Domain.Common;
using LovePlus.Domain.Identity;
using LovePlus.Domain.Pairing;
using LovePlus.Domain.Realtime;
using Microsoft.EntityFrameworkCore;

namespace LovePlus.Infrastructure.Persistence;

public sealed class LovePlusDbContext(DbContextOptions<LovePlusDbContext> options)
    : DbContext(options), IApplicationDbContext
{
    public DbSet<User> Users => Set<User>();
    public DbSet<DeviceSession> DeviceSessions => Set<DeviceSession>();
    public DbSet<DevicePushToken> DevicePushTokens => Set<DevicePushToken>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<CouplePair> CouplePairs => Set<CouplePair>();
    public DbSet<PairMember> PairMembers => Set<PairMember>();
    public DbSet<PairingCode> PairingCodes => Set<PairingCode>();
    public DbSet<UserLiveStatusSnapshot> UserLiveStatusSnapshots => Set<UserLiveStatusSnapshot>();
    public DbSet<CriticalBatteryEvent> CriticalBatteryEvents => Set<CriticalBatteryEvent>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    public async Task<TResult> ExecuteInTransactionAsync<TResult>(
        IsolationLevel isolationLevel,
        Func<CancellationToken, Task<TResult>> operation,
        CancellationToken cancellationToken)
    {
        var strategy = Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async token =>
        {
            await using var transaction = await Database.BeginTransactionAsync(isolationLevel, token);
            var result = await operation(token);
            await transaction.CommitAsync(token);
            return result;
        }, cancellationToken);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasPostgresExtension("postgis");

        modelBuilder.Entity<User>(entity =>
        {
            entity.ToTable("users");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Email).HasMaxLength(254);
            entity.Property(x => x.NormalizedEmail).HasMaxLength(254);
            entity.Property(x => x.DisplayName).HasMaxLength(80);
            entity.Property(x => x.PasswordHash).HasMaxLength(512);
            entity.HasIndex(x => x.NormalizedEmail).IsUnique();
        });

        modelBuilder.Entity<DeviceSession>(entity =>
        {
            entity.ToTable("device_sessions");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.DeviceId).HasMaxLength(128);
            entity.Property(x => x.DeviceName).HasMaxLength(120);
            entity.HasIndex(x => new { x.UserId, x.DeviceId });
            entity.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<DevicePushToken>(entity =>
        {
            entity.ToTable("device_push_tokens");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Platform).HasMaxLength(16);
            entity.Property(x => x.Token).HasMaxLength(512);
            entity.HasIndex(x => x.Token).IsUnique();
            entity.HasIndex(x => new { x.UserId, x.DisabledAtUtc });
            entity.HasIndex(x => x.DeviceSessionId).IsUnique();
            entity.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.DeviceSession).WithMany().HasForeignKey(x => x.DeviceSessionId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<RefreshToken>(entity =>
        {
            entity.ToTable("refresh_tokens");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.TokenHash).HasMaxLength(64).IsFixedLength();
            entity.HasIndex(x => x.TokenHash).IsUnique();
            entity.HasIndex(x => new { x.DeviceSessionId, x.ExpiresAtUtc });
            entity.HasOne(x => x.DeviceSession)
                .WithMany(x => x.RefreshTokens)
                .HasForeignKey(x => x.DeviceSessionId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<CouplePair>(entity =>
        {
            entity.ToTable("couple_pairs");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Status).HasConversion<string>().HasMaxLength(32);
            entity.HasIndex(x => x.Status);
        });

        modelBuilder.Entity<PairMember>(entity =>
        {
            entity.ToTable("pair_members");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => new { x.PairId, x.UserId }).IsUnique();
            entity.HasIndex(x => x.UserId).IsUnique().HasFilter("\"IsActive\" = TRUE");
            entity.HasOne(x => x.Pair).WithMany(x => x.Members).HasForeignKey(x => x.PairId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<PairingCode>(entity =>
        {
            entity.ToTable("pairing_codes");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.CodeHash).HasMaxLength(64).IsFixedLength();
            entity.HasIndex(x => x.CodeHash).IsUnique();
            entity.HasIndex(x => new { x.CreatorUserId, x.ExpiresAtUtc });
            entity.HasOne(x => x.CreatorUser).WithMany().HasForeignKey(x => x.CreatorUserId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.ConsumedByUser).WithMany().HasForeignKey(x => x.ConsumedByUserId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.CouplePair).WithMany().HasForeignKey(x => x.CouplePairId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<UserLiveStatusSnapshot>(entity =>
        {
            entity.ToTable("user_live_status_snapshots");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Location).HasColumnType("geography (point)").IsRequired(false);
            entity.Property(x => x.DeviceId).HasMaxLength(128);
            entity.Property(x => x.Mood).HasConversion<string>().HasMaxLength(32);
            entity.Property(x => x.IsMoodSharingEnabled).HasDefaultValue(true);
            entity.Property(x => x.BatteryState).HasConversion<string>().HasMaxLength(32);
            entity.Property(x => x.ActivityType).HasConversion<string>().HasMaxLength(32);
            entity.Property(x => x.Source).HasConversion<string>().HasMaxLength(32);
            entity.HasIndex(x => x.UserId).IsUnique();
            entity.HasIndex(x => x.PairId);
            entity.HasIndex(x => x.Location).HasMethod("gist");
        });

        modelBuilder.Entity<CriticalBatteryEvent>(entity =>
        {
            entity.ToTable("critical_battery_events");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.LastKnownLocation).HasColumnType("geography (point)");
            entity.HasIndex(x => new { x.UserId, x.PairId })
                .IsUnique()
                .HasFilter("\"RecoveredAtUtc\" IS NULL");
            entity.HasIndex(x => x.PairId);
        });

        modelBuilder.Entity<OutboxMessage>(entity =>
        {
            entity.ToTable("outbox_messages");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Type).HasMaxLength(200);
            entity.Property(x => x.Payload).HasColumnType("jsonb");
            entity.Property(x => x.LastError).HasMaxLength(2000);
            entity.HasIndex(x => new { x.ProcessedAtUtc, x.OccurredAtUtc });
        });
    }
}
