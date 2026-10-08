using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using LovePlus.Application.Identity;
using LovePlus.Application.Pairing;

namespace LovePlus.Api.IntegrationTests;

public sealed class PostgresPairingApiTests(LovePlusApiFactory factory) : IClassFixture<LovePlusApiFactory>
{
    [Fact]
    public async Task Redeem_pairing_code_succeeds_with_Npgsql_retry_execution_strategy()
    {
        // Local contributors keep the fast in-memory default. CI sets LOVEPLUS_TEST_POSTGRES,
        // which makes this exact HTTP flow run against migrated PostgreSQL/PostGIS with the
        // application's production EnableRetryOnFailure configuration.
        if (!factory.UsesPostgres)
        {
            return;
        }

        using var creatorClient = factory.CreateClient();
        using var redeemerClient = factory.CreateClient();

        var creator = await Register(creatorClient, "Pair Creator");
        creatorClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", creator.AccessToken);

        var createCodeResponse = await creatorClient.PostAsync("/api/pairing/code", null);
        createCodeResponse.EnsureSuccessStatusCode();
        var pairingCode = await createCodeResponse.Content.ReadFromJsonAsync<PairingCodeDto>();
        Assert.NotNull(pairingCode);

        var redeemer = await Register(redeemerClient, "Pair Redeemer");
        redeemerClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", redeemer.AccessToken);

        var redeemResponse = await redeemerClient.PostAsJsonAsync("/api/pairing/redeem", new
        {
            code = pairingCode.Code
        });
        var redeemedPair = await redeemResponse.Content.ReadFromJsonAsync<PairDto>();

        Assert.Equal(HttpStatusCode.OK, redeemResponse.StatusCode);
        Assert.NotNull(redeemedPair);
        Assert.Equal("Pair Creator", redeemedPair.PartnerDisplayName);

        var redeemerCurrent = await redeemerClient.GetFromJsonAsync<PairDto>("/api/pairing/current");
        Assert.NotNull(redeemerCurrent);
        Assert.Equal(redeemedPair.PairId, redeemerCurrent.PairId);
        Assert.Equal("Pair Creator", redeemerCurrent.PartnerDisplayName);

        creatorClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", creator.AccessToken);
        var creatorCurrent = await creatorClient.GetFromJsonAsync<PairDto>("/api/pairing/current");
        Assert.NotNull(creatorCurrent);
        Assert.Equal(redeemedPair.PairId, creatorCurrent.PairId);
        Assert.Equal("Pair Redeemer", creatorCurrent.PartnerDisplayName);
    }

    private static async Task<AuthTokens> Register(HttpClient client, string displayName)
    {
        var response = await client.PostAsJsonAsync("/api/auth/register", new
        {
            email = $"pairing-{Guid.NewGuid():N}@example.com",
            password = "a-secure-test-password",
            displayName,
            deviceId = $"pairing-device-{Guid.NewGuid():N}",
            deviceName = "Pairing Integration Device"
        });

        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<AuthTokens>())!;
    }
}
