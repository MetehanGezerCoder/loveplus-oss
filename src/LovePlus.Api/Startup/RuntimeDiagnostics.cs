namespace LovePlus.Api.Startup;

/// <summary>
/// Operational facts the mobile diagnostics screen needs in order to tell an honest story
/// about why something is not working. It deliberately contains no connection strings,
/// keys, tokens, coordinates or account data.
/// </summary>
public sealed record RuntimeDiagnostics(
    string Environment,
    string ApiVersion,
    bool UsesRelationalDatabase,
    bool UsesRedis,
    bool MigrationsApplied,
    string PushProvider,
    bool PushConfigured,
    bool RequiresHttps,
    int PresenceOnlineSeconds,
    int PresenceRecentlyOnlineSeconds,
    int ClientStatusHeartbeatSeconds,
    DateTimeOffset ServerTimeUtc);

/// <summary>Mutable startup facts collected before the first request is served.</summary>
public sealed class RuntimeDiagnosticsState
{
    public required string ApiVersion { get; init; }
    public required bool UsesRelationalDatabase { get; init; }
    public required bool UsesRedis { get; init; }
    public required string PushProvider { get; init; }
    public required bool PushConfigured { get; init; }
    public required bool RequiresHttps { get; init; }
    public required int PresenceOnlineSeconds { get; init; }
    public required int PresenceRecentlyOnlineSeconds { get; init; }
    public required int ClientStatusHeartbeatSeconds { get; init; }
    public bool MigrationsApplied { get; set; }
}
