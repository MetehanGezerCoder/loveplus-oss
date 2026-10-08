using System.Collections.Concurrent;
using LovePlus.Application.Common.Abstractions;
using LovePlus.Application.Realtime;
using LovePlus.Domain.Identity;
using LovePlus.Domain.Realtime;
using LovePlus.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace LovePlus.Application.Tests;

internal static class TestPolicies
{
    public static RealtimeStatusPolicy Realtime(bool simulator = false) => new(
        TimeSpan.FromMinutes(12),
        TimeSpan.FromMinutes(2),
        TimeSpan.FromMinutes(10),
        TimeSpan.FromMinutes(10),
        TimeSpan.FromHours(24),
        100,
        simulator);
}

internal static class TestDb
{
    public static LovePlusDbContext Create()
    {
        var options = new DbContextOptionsBuilder<LovePlusDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(x => x.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new LovePlusDbContext(options);
    }
}

internal sealed class TestClock(DateTimeOffset now) : IClock
{
    public DateTimeOffset UtcNow { get; set; } = now;
}

internal sealed class TestTokenService : ITokenService
{
    private int _counter;
    public TimeSpan AccessTokenLifetime => TimeSpan.FromMinutes(15);
    public TimeSpan TelemetryTokenLifetime => TimeSpan.FromDays(7);
    public TimeSpan RefreshTokenLifetime => TimeSpan.FromDays(30);
    public string CreateAccessToken(User user, DeviceSession session, DateTimeOffset now) =>
        $"access-{user.Id}-{session.Id}-{_counter}";
    public string CreateTelemetryToken(User user, DeviceSession session, DateTimeOffset now) =>
        $"telemetry-{user.Id}-{session.Id}-{_counter}";
    public string GenerateRefreshToken() => $"refresh-{Interlocked.Increment(ref _counter)}";
    public string HashSecret(string secret) => Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(
        System.Text.Encoding.UTF8.GetBytes(secret)));
    public string GeneratePairingCode() => $"ABCD-{Interlocked.Increment(ref _counter):D4}";
}

internal sealed class TestPasswordService : IPasswordService
{
    public string Hash(User user, string password) => $"hashed:{password}";
    public bool Verify(User user, string hash, string password) => hash == $"hashed:{password}";
}

internal sealed class TestLiveStatusStore : ILiveStatusStore
{
    private readonly ConcurrentDictionary<Guid, UserLiveStatus> _items = new();
    public Task<UserLiveStatus?> GetAsync(Guid userId, CancellationToken cancellationToken) =>
        Task.FromResult(_items.TryGetValue(userId, out var value) ? value : null);

    public Task<bool> TrySetAsync(UserLiveStatus status, TimeSpan ttl, CancellationToken cancellationToken)
    {
        while (true)
        {
            if (!_items.TryGetValue(status.UserId, out var current))
            {
                if (_items.TryAdd(status.UserId, status)) return Task.FromResult(true);
                continue;
            }
            if ((current.DeviceId == status.DeviceId && current.SequenceNumber >= status.SequenceNumber)
                || (current.DeviceId != status.DeviceId && current.RecordedAtUtc >= status.RecordedAtUtc))
            {
                return Task.FromResult(false);
            }
            if (_items.TryUpdate(status.UserId, status, current)) return Task.FromResult(true);
        }
    }

    public Task<UserLiveStatus?> UpdateMoodAsync(Guid userId, MoodType mood, bool isMoodSharingEnabled, TimeSpan ttl, CancellationToken cancellationToken)
    {
        if (!_items.TryGetValue(userId, out var current)) return Task.FromResult<UserLiveStatus?>(null);
        var updated = current with { Mood = mood, IsMoodSharingEnabled = isMoodSharingEnabled };
        _items[userId] = updated;
        return Task.FromResult<UserLiveStatus?>(updated);
    }
}

internal sealed class TestPublisher : IPartnerStatusPublisher
{
    public List<PartnerStatusDto> Statuses { get; } = [];
    public List<CriticalBatteryDto> CriticalEvents { get; } = [];
    public Task PublishStatusAsync(Guid pairId, PartnerStatusDto status, CancellationToken cancellationToken)
    {
        Statuses.Add(status);
        return Task.CompletedTask;
    }
    public Task PublishCriticalBatteryAsync(Guid pairId, CriticalBatteryDto criticalBattery, CancellationToken cancellationToken)
    {
        CriticalEvents.Add(criticalBattery);
        return Task.CompletedTask;
    }
}

internal sealed class TestPushSender : IPushNotificationSender
{
    public int Count { get; private set; }
    public Task SendCriticalBatteryAsync(Guid partnerUserId, CriticalBatteryDto notification, CancellationToken cancellationToken)
    {
        Count++;
        return Task.CompletedTask;
    }
}
