namespace LovePlus.Infrastructure.Notifications;

public static class PushProviders
{
    public const string Firebase = "firebase";

    /// <summary>
    /// An operator statement that this deployment knowingly runs without push delivery.
    /// It is deliberately explicit: an unset provider fails startup instead of silently
    /// degrading to "no notifications".
    /// </summary>
    public const string Disabled = "disabled";

    /// <summary>Development/testing only; never selectable in Production.</summary>
    public const string Development = "development";
}

public sealed class PushNotificationOptions
{
    public const string SectionName = "Push";

    public string Provider { get; init; } = string.Empty;
    public FirebasePushOptions Firebase { get; init; } = new();
}

public sealed class FirebasePushOptions
{
    /// <summary>Raw service-account JSON, normally injected from a secret store.</summary>
    public string? ServiceAccountJson { get; init; }

    /// <summary>Path to a mounted service-account JSON file.</summary>
    public string? ServiceAccountJsonPath { get; init; }

    /// <summary>Overrides the project id found in the service account.</summary>
    public string? ProjectId { get; init; }

    public string? ReadServiceAccountJson()
    {
        if (!string.IsNullOrWhiteSpace(ServiceAccountJson))
        {
            return ServiceAccountJson;
        }
        if (!string.IsNullOrWhiteSpace(ServiceAccountJsonPath) && File.Exists(ServiceAccountJsonPath))
        {
            return File.ReadAllText(ServiceAccountJsonPath);
        }
        return null;
    }

    public bool IsConfigured() => ReadServiceAccountJson() is not null;
}
