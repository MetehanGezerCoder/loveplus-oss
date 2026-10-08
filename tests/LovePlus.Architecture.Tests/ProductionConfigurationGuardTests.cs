using LovePlus.Api.Startup;
using LovePlus.Infrastructure.Identity;
using LovePlus.Infrastructure.Notifications;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;

namespace LovePlus.Architecture.Tests;

public sealed class ProductionConfigurationGuardTests
{
    private static readonly JwtOptions RealJwt = new()
    {
        Issuer = "loveplus-api",
        Audience = "loveplus-mobile",
        SigningKey = new string('k', 64)
    };

    [Fact]
    public void Production_rejects_the_development_in_memory_infrastructure()
    {
        var error = Assert.Throws<InvalidOperationException>(() => Validate(
            Production(),
            Settings(("Demo:UseInMemoryInfrastructure", "true"))));

        Assert.Contains("Demo__UseInMemoryInfrastructure", error.Message);
    }

    [Fact]
    public void Production_rejects_a_placeholder_signing_key()
    {
        var jwt = new JwtOptions
        {
            Issuer = "loveplus-api",
            Audience = "loveplus-mobile",
            SigningKey = "replace-with-at-least-64-random-characters-before-running-the-api!!"
        };

        var error = Assert.Throws<InvalidOperationException>(() => Validate(Production(), Settings(), jwt));

        Assert.Contains("Jwt__SigningKey", error.Message);
    }

    [Fact]
    public void Production_rejects_a_placeholder_database_password()
    {
        var error = Assert.Throws<InvalidOperationException>(() => Validate(
            Production(),
            Settings(("ConnectionStrings:Postgres", "Host=db;Password=replace-with-a-local-development-password"))));

        Assert.Contains("placeholder password", error.Message);
    }

    [Fact]
    public void Production_rejects_an_undeclared_push_provider()
    {
        var error = Assert.Throws<InvalidOperationException>(() => Validate(
            Production(),
            Settings(),
            push: new PushNotificationOptions { Provider = string.Empty }));

        Assert.Contains("Push__Provider", error.Message);
    }

    [Fact]
    public void Production_rejects_firebase_without_credentials()
    {
        var error = Assert.Throws<InvalidOperationException>(() => Validate(
            Production(),
            Settings(),
            push: new PushNotificationOptions { Provider = PushProviders.Firebase }));

        Assert.Contains("ServiceAccountJson", error.Message);
    }

    [Fact]
    public void Production_accepts_an_explicitly_disabled_push_provider()
    {
        Validate(Production(), Settings());
    }

    [Theory]
    [InlineData("Development")]
    [InlineData("Production")]
    public void Presence_window_must_tolerate_one_missed_client_heartbeat(string environmentName)
    {
        var error = Assert.Throws<InvalidOperationException>(() => Validate(
            new FakeEnvironment(environmentName),
            Settings(),
            presenceOnlineSeconds: 90,
            clientHeartbeatSeconds: 60));

        Assert.Contains("Realtime__OnlineSeconds", error.Message);
    }

    [Fact]
    public void Development_is_allowed_to_use_the_in_memory_demo_infrastructure()
    {
        Validate(
            new FakeEnvironment(Environments.Development),
            Settings(("Demo:UseInMemoryInfrastructure", "true"), ("Demo:SeedEnabled", "true")),
            push: new PushNotificationOptions { Provider = string.Empty });
    }

    private static void Validate(
        IHostEnvironment environment,
        IConfiguration configuration,
        JwtOptions? jwt = null,
        PushNotificationOptions? push = null,
        int presenceOnlineSeconds = 180,
        int clientHeartbeatSeconds = 60) =>
        ProductionConfigurationGuard.Validate(
            configuration,
            environment,
            jwt ?? RealJwt,
            push ?? new PushNotificationOptions { Provider = PushProviders.Disabled },
            presenceOnlineSeconds,
            clientHeartbeatSeconds);

    private static IHostEnvironment Production() => new FakeEnvironment(Environments.Production);

    private static IConfiguration Settings(params (string Key, string Value)[] overrides)
    {
        var values = new Dictionary<string, string?>
        {
            ["ConnectionStrings:Postgres"] = "Host=db;Database=loveplus;Username=loveplus;Password=s3cret",
            ["ConnectionStrings:Redis"] = "redis:6379,password=s3cret,abortConnect=false"
        };
        foreach (var (key, value) in overrides)
        {
            values[key] = value;
        }
        return new ConfigurationBuilder().AddInMemoryCollection(values).Build();
    }

    private sealed class FakeEnvironment(string environmentName) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = environmentName;
        public string ApplicationName { get; set; } = "LovePlus.Api";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
