using LovePlus.Application.Realtime;
using LovePlus.Domain.Realtime;

namespace LovePlus.Application.Tests;

/// <summary>
/// A client caches the last pushed partner DTO. If the DTO only carried a presence verdict,
/// the dashboard would keep showing "online" for a phone that stopped reporting minutes ago,
/// so the windows that produced the verdict travel with it.
/// </summary>
public sealed class PresenceFreshnessTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 15, 9, 0, 0, TimeSpan.Zero);

    private static readonly RealtimeStatusPolicy Policy = new(
        LiveStatusTtl: TimeSpan.FromMinutes(12),
        OnlineWindow: TimeSpan.FromMinutes(3),
        RecentlyOnlineWindow: TimeSpan.FromMinutes(10),
        LocationFreshnessWindow: TimeSpan.FromMinutes(10),
        MaxOfflinePacketAge: TimeSpan.FromHours(24),
        ApproximateAccuracyMeters: 100,
        SimulatorEnabled: false);

    [Fact]
    public void Partner_status_carries_the_windows_that_produced_the_presence_verdict()
    {
        var dto = Map(Now);

        Assert.Equal(PresenceState.Online, dto.Presence);
        Assert.Equal(180, dto.PresenceOnlineSeconds);
        Assert.Equal(600, dto.PresenceRecentlyOnlineSeconds);
    }

    [Theory]
    [InlineData(0, PresenceState.Online)]
    [InlineData(179, PresenceState.Online)]
    [InlineData(181, PresenceState.RecentlyOnline)]
    [InlineData(599, PresenceState.RecentlyOnline)]
    [InlineData(601, PresenceState.Offline)]
    public void Presence_degrades_with_the_age_of_the_last_real_device_packet(int ageSeconds, PresenceState expected)
    {
        var dto = Map(Now.AddSeconds(-ageSeconds));

        Assert.Equal(expected, dto.Presence);
    }

    [Fact]
    public void A_client_re_deriving_presence_from_the_dto_reaches_the_same_verdict_as_the_server()
    {
        var recordedAt = Now.AddSeconds(-240);
        var dto = Map(recordedAt);

        // The mobile client applies exactly this rule to its cached copy.
        var age = Now - dto.RecordedAtUtc;
        var clientVerdict = age.TotalSeconds <= dto.PresenceOnlineSeconds
            ? PresenceState.Online
            : age.TotalSeconds <= dto.PresenceRecentlyOnlineSeconds
                ? PresenceState.RecentlyOnline
                : PresenceState.Offline;

        Assert.Equal(dto.Presence, clientVerdict);
        Assert.Equal(PresenceState.RecentlyOnline, clientVerdict);
    }

    private static PartnerStatusDto Map(DateTimeOffset recordedAt) => PartnerStatusMapper.Map(
        new UserLiveStatus(
            Guid.NewGuid(),
            Guid.NewGuid(),
            null,
            IsLocationSharingEnabled: false,
            ShareLastKnownLocation: false,
            BatteryLevel: 74,
            IsCharging: false,
            BatteryState.Discharging,
            ActivityType.Stationary,
            MoodType.InLove,
            recordedAt,
            "device-1",
            SequenceNumber: 12,
            TelemetrySource.RealDevice),
        "Meliha",
        counterpart: null,
        Policy,
        new GeoDistanceCalculator(),
        Now);
}
