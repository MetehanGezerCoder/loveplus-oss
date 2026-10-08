using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using LovePlus.Application.Common.Abstractions;
using LovePlus.Application.Realtime;
using LovePlus.Domain.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace LovePlus.Infrastructure.Notifications;

/// <summary>
/// Production FCM HTTP v1 adapter. It only ever sends privacy-safe critical-battery
/// notifications: a level and a boolean saying whether a last-known location exists.
/// Coordinates, moods, distances and heartbeat waveforms are never part of a push payload,
/// and a heartbeat is never replayed through this channel.
/// </summary>
public sealed class FirebasePushNotificationSender(
    IApplicationDbContext db,
    GoogleServiceAccountTokenProvider tokens,
    HttpClient httpClient,
    IOptions<PushNotificationOptions> options,
    IClock clock,
    ILogger<FirebasePushNotificationSender> logger) : IPushNotificationSender
{
    private readonly FirebasePushOptions _firebase = options.Value.Firebase;

    public async Task SendCriticalBatteryAsync(
        Guid partnerUserId,
        CriticalBatteryDto notification,
        CancellationToken cancellationToken)
    {
        var json = _firebase.ReadServiceAccountJson();
        if (json is null)
        {
            // Startup refuses to select this adapter without credentials, so reaching here
            // means configuration changed underneath a running process.
            throw new InvalidOperationException(
                "Firebase push is selected but no service account is configured.");
        }

        var account = GoogleServiceAccount.Parse(json);
        var projectId = string.IsNullOrWhiteSpace(_firebase.ProjectId) ? account.ProjectId : _firebase.ProjectId;
        var registrations = await db.DevicePushTokens.AsNoTracking()
            .Where(x => x.UserId == partnerUserId && x.DisabledAtUtc == null)
            .Select(x => new { x.Id, x.Token })
            .ToListAsync(cancellationToken);
        if (registrations.Count == 0)
        {
            logger.LogInformation(
                "No active push registration for the notified partner; critical battery event {EventId} stays in-app only",
                notification.EventId);
            return;
        }

        var accessToken = await tokens.GetAccessTokenAsync(account, cancellationToken);
        var endpoint = $"https://fcm.googleapis.com/v1/projects/{projectId}/messages:send";
        var retiredTokenIds = new List<Guid>();

        foreach (var registration in registrations)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, endpoint);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            request.Content = JsonContent.Create(new
            {
                message = new
                {
                    token = registration.Token,
                    notification = new
                    {
                        title = "Partnerinin pili kritik",
                        body = $"Pil seviyesi %{notification.BatteryLevel}."
                    },
                    data = new Dictionary<string, string>
                    {
                        ["type"] = "criticalBattery",
                        ["eventId"] = notification.EventId.ToString(),
                        ["batteryLevel"] = notification.BatteryLevel.ToString(),
                        ["lastKnownLocationAvailable"] = notification.LastKnownLocationAvailable
                            ? "true"
                            : "false"
                    },
                    android = new { priority = "high" }
                }
            });

            using var response = await httpClient.SendAsync(request, cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                continue;
            }
            if (IsPermanentTokenFailure(response.StatusCode, await SafeReadBody(response, cancellationToken)))
            {
                retiredTokenIds.Add(registration.Id);
                continue;
            }
            logger.LogWarning(
                "FCM delivery for critical battery event {EventId} failed with status {Status}",
                notification.EventId,
                (int)response.StatusCode);
        }

        if (retiredTokenIds.Count > 0)
        {
            await RetireAsync(retiredTokenIds, cancellationToken);
        }
    }

    private async Task RetireAsync(IReadOnlyCollection<Guid> tokenIds, CancellationToken cancellationToken)
    {
        var now = clock.UtcNow;
        var stale = await db.DevicePushTokens.Where(x => tokenIds.Contains(x.Id)).ToListAsync(cancellationToken);
        foreach (var entry in stale)
        {
            entry.DisabledAtUtc = now;
            entry.UpdatedAtUtc = now;
        }
        await db.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Retired {Count} unregistered push tokens reported by FCM", stale.Count);
    }

    private static async Task<string> SafeReadBody(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        try
        {
            return await response.Content.ReadAsStringAsync(cancellationToken);
        }
        catch (HttpRequestException)
        {
            return string.Empty;
        }
    }

    private static bool IsPermanentTokenFailure(HttpStatusCode status, string body)
    {
        if (status == HttpStatusCode.NotFound)
        {
            return true;
        }
        if (status != HttpStatusCode.BadRequest)
        {
            return false;
        }
        try
        {
            using var document = JsonDocument.Parse(body);
            return document.RootElement.TryGetProperty("error", out var error)
                && error.TryGetProperty("status", out var value)
                && value.GetString() == "INVALID_ARGUMENT";
        }
        catch (JsonException)
        {
            return false;
        }
    }
}

/// <summary>
/// Selected only when an operator explicitly sets <c>Push__Provider=disabled</c>. It records
/// that no push provider exists so nothing in the product can claim a delivery it did not make.
/// </summary>
public sealed class DisabledPushNotificationSender(
    ILogger<DisabledPushNotificationSender> logger) : IPushNotificationSender
{
    public Task SendCriticalBatteryAsync(
        Guid partnerUserId,
        CriticalBatteryDto notification,
        CancellationToken cancellationToken)
    {
        logger.LogWarning(
            "Push delivery is disabled by configuration; critical battery event {EventId} stays in-app only",
            notification.EventId);
        return Task.CompletedTask;
    }
}
