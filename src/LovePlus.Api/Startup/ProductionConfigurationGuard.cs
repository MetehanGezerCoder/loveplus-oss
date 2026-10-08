using LovePlus.Infrastructure.Identity;
using LovePlus.Infrastructure.Notifications;

namespace LovePlus.Api.Startup;

/// <summary>
/// Refuses to start a Production process that is only accidentally safe. Every check here
/// protects a boundary that silently degrades otherwise: a placeholder signing key, a demo
/// in-memory store standing in for PostgreSQL, or a presence window that reports "online"
/// for a phone that stopped reporting.
/// </summary>
public static class ProductionConfigurationGuard
{
    private const string PlaceholderMarker = "replace-with";

    public static void Validate(
        IConfiguration configuration,
        IHostEnvironment environment,
        JwtOptions jwt,
        PushNotificationOptions push,
        int presenceOnlineSeconds,
        int clientHeartbeatSeconds)
    {
        var problems = new List<string>();

        if (presenceOnlineSeconds < clientHeartbeatSeconds * 2)
        {
            problems.Add(
                $"Realtime__OnlineSeconds ({presenceOnlineSeconds}) must be at least twice "
                + $"Realtime__ClientStatusHeartbeatSeconds ({clientHeartbeatSeconds}); otherwise a single "
                + "missed telemetry beat reports a connected partner as offline.");
        }

        if (!environment.IsProduction())
        {
            ThrowIfAny(problems);
            return;
        }

        if (configuration.GetValue("Demo:UseInMemoryInfrastructure", false))
        {
            problems.Add("Demo__UseInMemoryInfrastructure must be false in Production.");
        }
        if (configuration.GetValue("Demo:SeedEnabled", false))
        {
            problems.Add("Demo__SeedEnabled must be false in Production.");
        }
        if (configuration.GetValue("Realtime:EnableSimulator", false))
        {
            problems.Add("Realtime__EnableSimulator must be false in Production.");
        }
        if (string.IsNullOrWhiteSpace(configuration.GetConnectionString("Postgres")))
        {
            problems.Add("ConnectionStrings__Postgres must be configured in Production.");
        }
        if (string.IsNullOrWhiteSpace(configuration.GetConnectionString("Redis")))
        {
            problems.Add("ConnectionStrings__Redis must be configured in Production.");
        }
        if (ContainsPlaceholder(jwt.SigningKey))
        {
            problems.Add("Jwt__SigningKey still contains an example placeholder value.");
        }
        if (ContainsPlaceholder(configuration.GetConnectionString("Postgres"))
            || ContainsPlaceholder(configuration.GetConnectionString("Redis")))
        {
            problems.Add("A connection string still contains an example placeholder password.");
        }

        switch (push.Provider?.Trim().ToLowerInvariant())
        {
            case PushProviders.Firebase when !push.Firebase.IsConfigured():
                problems.Add(
                    "Push__Provider=firebase requires Push__Firebase__ServiceAccountJson or "
                    + "Push__Firebase__ServiceAccountJsonPath.");
                break;
            case PushProviders.Firebase:
            case PushProviders.Disabled:
                break;
            default:
                problems.Add(
                    "Push__Provider must be set to 'firebase' (with Firebase credentials) or explicitly "
                    + "to 'disabled'. Love+ does not start with an undeclared notification provider.");
                break;
        }

        ThrowIfAny(problems);
    }

    private static bool ContainsPlaceholder(string? value) =>
        value is not null && value.Contains(PlaceholderMarker, StringComparison.OrdinalIgnoreCase);

    private static void ThrowIfAny(IReadOnlyCollection<string> problems)
    {
        if (problems.Count == 0)
        {
            return;
        }
        throw new InvalidOperationException(
            "Love+ configuration is not deployable:" + Environment.NewLine
            + string.Join(Environment.NewLine, problems.Select(x => $"  - {x}")));
    }
}
