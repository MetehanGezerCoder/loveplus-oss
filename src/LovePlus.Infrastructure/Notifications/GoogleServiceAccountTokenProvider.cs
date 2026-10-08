using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using LovePlus.Application.Common.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace LovePlus.Infrastructure.Notifications;

public sealed record GoogleServiceAccount(
    [property: JsonPropertyName("project_id")] string ProjectId,
    [property: JsonPropertyName("client_email")] string ClientEmail,
    [property: JsonPropertyName("private_key")] string PrivateKey,
    [property: JsonPropertyName("token_uri")] string? TokenUri)
{
    public string ResolvedTokenUri => string.IsNullOrWhiteSpace(TokenUri)
        ? "https://oauth2.googleapis.com/token"
        : TokenUri;

    public static GoogleServiceAccount Parse(string json)
    {
        var account = JsonSerializer.Deserialize<GoogleServiceAccount>(json)
            ?? throw new InvalidOperationException("The Firebase service account JSON could not be parsed.");
        if (string.IsNullOrWhiteSpace(account.ProjectId)
            || string.IsNullOrWhiteSpace(account.ClientEmail)
            || string.IsNullOrWhiteSpace(account.PrivateKey))
        {
            throw new InvalidOperationException(
                "The Firebase service account JSON must contain project_id, client_email and private_key.");
        }
        return account;
    }
}

/// <summary>
/// Exchanges a service-account assertion for a short-lived FCM access token and caches it
/// until shortly before expiry. The private key never leaves this process and is never logged.
/// Registered as a singleton so the cache actually survives between notifications; the
/// HttpClient comes from the factory per call so connection recycling still applies.
/// </summary>
public sealed class GoogleServiceAccountTokenProvider(IHttpClientFactory httpClientFactory, IClock clock)
{
    public const string HttpClientName = "google-oauth";
    private const string MessagingScope = "https://www.googleapis.com/auth/firebase.messaging";
    private static readonly TimeSpan RenewBefore = TimeSpan.FromMinutes(2);
    private readonly SemaphoreSlim _gate = new(1, 1);
    private string? _cachedToken;
    private DateTimeOffset _cachedUntil = DateTimeOffset.MinValue;

    public async Task<string> GetAccessTokenAsync(GoogleServiceAccount account, CancellationToken ct)
    {
        if (_cachedToken is not null && clock.UtcNow + RenewBefore < _cachedUntil)
        {
            return _cachedToken;
        }

        await _gate.WaitAsync(ct);
        try
        {
            if (_cachedToken is not null && clock.UtcNow + RenewBefore < _cachedUntil)
            {
                return _cachedToken;
            }

            var now = clock.UtcNow;
            var assertion = CreateAssertion(account, now);
            var httpClient = httpClientFactory.CreateClient(HttpClientName);
            using var request = new HttpRequestMessage(HttpMethod.Post, account.ResolvedTokenUri)
            {
                Content = new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["grant_type"] = "urn:ietf:params:oauth:grant-type:jwt-bearer",
                    ["assertion"] = assertion
                })
            };
            using var response = await httpClient.SendAsync(request, ct);
            if (!response.IsSuccessStatusCode)
            {
                throw new InvalidOperationException(
                    $"Google token exchange failed with status {(int)response.StatusCode}.");
            }

            using var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
            var token = payload.RootElement.GetProperty("access_token").GetString()
                ?? throw new InvalidOperationException("Google token exchange returned no access token.");
            var expiresIn = payload.RootElement.TryGetProperty("expires_in", out var seconds)
                ? seconds.GetInt32()
                : 3_600;
            _cachedToken = token;
            _cachedUntil = now.AddSeconds(expiresIn);
            return token;
        }
        finally
        {
            _gate.Release();
        }
    }

    private static string CreateAssertion(GoogleServiceAccount account, DateTimeOffset now)
    {
        var issuedAt = now.ToUnixTimeSeconds();
        var header = Base64Url(JsonSerializer.SerializeToUtf8Bytes(new { alg = "RS256", typ = "JWT" }));
        var claims = Base64Url(JsonSerializer.SerializeToUtf8Bytes(new
        {
            iss = account.ClientEmail,
            scope = MessagingScope,
            aud = account.ResolvedTokenUri,
            iat = issuedAt,
            exp = issuedAt + 3_600
        }));
        var unsigned = $"{header}.{claims}";
        using var rsa = RSA.Create();
        rsa.ImportFromPem(account.PrivateKey);
        var signature = rsa.SignData(
            Encoding.ASCII.GetBytes(unsigned),
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);
        return $"{unsigned}.{Base64Url(signature)}";
    }

    private static string Base64Url(ReadOnlySpan<byte> value) =>
        Convert.ToBase64String(value).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
