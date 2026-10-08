using LovePlus.Application.Realtime;
using Microsoft.Extensions.Logging;

namespace LovePlus.Infrastructure.Notifications;

public sealed class DevelopmentPushNotificationSender(
    ILogger<DevelopmentPushNotificationSender> logger) : IPushNotificationSender
{
    public Task SendCriticalBatteryAsync(
        Guid partnerUserId,
        CriticalBatteryDto notification,
        CancellationToken cancellationToken)
    {
        logger.LogInformation(
            "Development push suppressed for critical battery event {EventId}; configure an FCM implementation for production",
            notification.EventId);
        return Task.CompletedTask;
    }
}
