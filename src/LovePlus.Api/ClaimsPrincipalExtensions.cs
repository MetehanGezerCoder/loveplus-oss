using System.Security.Claims;

namespace LovePlus.Api;

public static class ClaimsPrincipalExtensions
{
    public static Guid RequiredUserId(this ClaimsPrincipal principal) =>
        RequiredGuidClaim(principal, "sub");

    public static Guid RequiredSessionId(this ClaimsPrincipal principal) =>
        RequiredGuidClaim(principal, "session_id");

    public static string RequiredDeviceId(this ClaimsPrincipal principal) =>
        principal.FindFirstValue("device_id")
        ?? throw new InvalidOperationException("Authenticated device claim is missing.");

    private static Guid RequiredGuidClaim(ClaimsPrincipal principal, string claimType) =>
        Guid.TryParse(principal.FindFirstValue(claimType), out var value)
            ? value
            : throw new InvalidOperationException($"Authenticated {claimType} claim is missing.");
}
