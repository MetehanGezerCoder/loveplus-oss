using System.Net;
using System.Net.Http.Json;
using LovePlus.Application.Identity;
using System.Net.Http.Headers;

namespace LovePlus.Api.IntegrationTests;

public sealed class AuthApiTests(LovePlusApiFactory factory) : IClassFixture<LovePlusApiFactory>
{
    [Fact]
    public async Task Register_and_login_use_real_HTTP_pipeline()
    {
        using var client = factory.CreateClient();
        var email = $"integration-{Guid.NewGuid():N}@example.com";
        var register = await client.PostAsJsonAsync("/api/auth/register", new
        {
            email,
            password = "a-secure-test-password",
            displayName = "Integration User",
            deviceId = "integration-device-1",
            deviceName = "Test Device"
        });
        var registered = await register.Content.ReadFromJsonAsync<AuthTokens>();
        var login = await client.PostAsJsonAsync("/api/auth/login", new
        {
            email,
            password = "a-secure-test-password",
            deviceId = "integration-device-2",
            deviceName = "Second Test Device"
        });

        Assert.Equal(HttpStatusCode.OK, register.StatusCode);
        Assert.NotNull(registered?.AccessToken);
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
    }

    [Fact]
    public async Task SignalR_negotiate_requires_authentication()
    {
        using var client = factory.CreateClient();
        var response = await client.PostAsync("/hubs/status/negotiate?negotiateVersion=1", null);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Heartbeat_requires_app_authentication_and_active_pair()
    {
        using var client = factory.CreateClient();
        var body = JsonContent.Create(new { eventId = Guid.NewGuid(), pattern = new[] { 0, 90 } });
        var anonymous = await client.PostAsync("/api/heartbeat/", body);
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);

        var tokens = await Register(client);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokens.AccessToken);
        var unpaired = await client.PostAsJsonAsync("/api/heartbeat/", new
        {
            eventId = Guid.NewGuid(),
            pattern = new[] { 0, 90 }
        });
        Assert.Equal(HttpStatusCode.Forbidden, unpaired.StatusCode);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokens.TelemetryToken);
        var telemetryToken = await client.PostAsJsonAsync("/api/heartbeat/", new
        {
            eventId = Guid.NewGuid(),
            pattern = new[] { 0, 90 }
        });
        Assert.Equal(HttpStatusCode.Forbidden, telemetryToken.StatusCode);
    }

    private static async Task<AuthTokens> Register(HttpClient client)
    {
        var response = await client.PostAsJsonAsync("/api/auth/register", new
        {
            email = $"heartbeat-{Guid.NewGuid():N}@example.com",
            password = "a-secure-test-password",
            displayName = "Heartbeat User",
            deviceId = $"heartbeat-device-{Guid.NewGuid():N}",
            deviceName = "Heartbeat Test Device"
        });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<AuthTokens>())!;
    }
}
