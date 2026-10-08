using LovePlus.Api;
using LovePlus.Application.Common.Abstractions;
using LovePlus.Application.Heartbeat;
using LovePlus.Application.Realtime;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace LovePlus.Api.Hubs;

[Authorize]
public sealed class StatusHub(IApplicationDbContext db, IRealtimePresenceTracker presence) : Hub
{
    public override async Task OnConnectedAsync()
    {
        var principal = Context.User ?? throw new HubException("Authentication is required.");
        var userId = principal.RequiredUserId();
        var pairId = await db.PairMembers.AsNoTracking()
            .Where(x => x.UserId == userId && x.IsActive)
            .Select(x => (Guid?)x.PairId)
            .SingleOrDefaultAsync(Context.ConnectionAborted);
        if (pairId is null)
        {
            Context.Abort();
            return;
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, PairGroup.Name(pairId.Value), Context.ConnectionAborted);
        await Groups.AddToGroupAsync(Context.ConnectionId, UserGroup.Name(userId), Context.ConnectionAborted);
        presence.Connected(userId, Context.ConnectionId);
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        if (Context.User is not null)
        {
            presence.Disconnected(Context.User.RequiredUserId(), Context.ConnectionId);
        }
        await base.OnDisconnectedAsync(exception);
    }
}

public static class PairGroup
{
    public static string Name(Guid pairId) => $"pair:{pairId:N}";
}

public static class UserGroup
{
    public static string Name(Guid userId) => $"user:{userId:N}";
}

public sealed class PartnerStatusPublisher(IHubContext<StatusHub> hub) : IPartnerStatusPublisher
{
    public Task PublishStatusAsync(Guid pairId, PartnerStatusDto status, CancellationToken ct) =>
        hub.Clients.Group(PairGroup.Name(pairId)).SendAsync("PartnerStatusUpdated", status, ct);

    public Task PublishCriticalBatteryAsync(Guid pairId, CriticalBatteryDto criticalBattery, CancellationToken ct) =>
        hub.Clients.Group(PairGroup.Name(pairId)).SendAsync("CriticalBattery", criticalBattery, ct);
}

public sealed class HeartbeatPublisher(IHubContext<StatusHub> hub) : IHeartbeatPublisher
{
    public Task PublishPatternAsync(Guid receiverUserId, HeartbeatPatternDto pattern, CancellationToken ct) =>
        hub.Clients.Group(UserGroup.Name(receiverUserId)).SendAsync("HeartbeatPatternReceived", pattern, ct);

    public Task PublishAcknowledgementAsync(
        Guid senderUserId,
        HeartbeatAcknowledgementDto acknowledgement,
        CancellationToken ct) =>
        hub.Clients.Group(UserGroup.Name(senderUserId)).SendAsync("HeartbeatAcknowledged", acknowledgement, ct);
}
