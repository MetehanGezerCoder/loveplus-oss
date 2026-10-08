using LovePlus.Application.Realtime;
using LovePlus.Infrastructure.Notifications;

namespace LovePlus.Api.Startup;

public sealed record PushProviderSelection(string Provider, bool Configured);

public static class PushNotificationSetup
{
    /// <summary>
    /// Chooses a push adapter from declared configuration. Development and Testing may fall back
    /// to the logging sender; Production must state its provider, and
    /// <see cref="ProductionConfigurationGuard"/> has already rejected an undeclared one.
    /// </summary>
    public static PushProviderSelection AddPushNotifications(
        this IServiceCollection services,
        PushNotificationOptions options,
        IHostEnvironment environment)
    {
        var declared = options.Provider?.Trim().ToLowerInvariant();
        if (declared == PushProviders.Firebase && options.Firebase.IsConfigured())
        {
            // The token provider is a singleton because its whole point is caching the access
            // token between notifications; it takes clients from the factory so pooling and
            // connection recycling still apply.
            services.AddHttpClient(GoogleServiceAccountTokenProvider.HttpClientName, client =>
                client.Timeout = TimeSpan.FromSeconds(15));
            services.AddSingleton<GoogleServiceAccountTokenProvider>();
            services.AddHttpClient<IPushNotificationSender, FirebasePushNotificationSender>(client =>
                client.Timeout = TimeSpan.FromSeconds(15));
            return new PushProviderSelection(PushProviders.Firebase, true);
        }

        if (declared == PushProviders.Disabled)
        {
            services.AddScoped<IPushNotificationSender, DisabledPushNotificationSender>();
            return new PushProviderSelection(PushProviders.Disabled, false);
        }

        if (environment.IsDevelopment() || environment.IsEnvironment("Testing"))
        {
            services.AddScoped<IPushNotificationSender, DevelopmentPushNotificationSender>();
            return new PushProviderSelection(PushProviders.Development, false);
        }

        throw new InvalidOperationException(
            "No push provider could be selected. Set Push__Provider to 'firebase' with credentials, "
            + "or explicitly to 'disabled'.");
    }
}
