using System;

namespace OpenClaw.Win.Core;

public sealed class SystemNotificationRouter
{
    private readonly NotificationRateLimiter _rateLimiter;

    public SystemNotificationRouter(NotificationRateLimiter rateLimiter)
    {
        _rateLimiter = rateLimiter ?? throw new ArgumentNullException(nameof(rateLimiter));
    }

    public bool Enabled { get; set; } = true;

    public bool TryBuild(SystemNotificationEvent notificationEvent, out SystemNotification notification)
    {
        notification = default!;
        if (!Enabled)
        {
            return false;
        }

        var key = string.IsNullOrWhiteSpace(notificationEvent.Key)
            ? notificationEvent.Kind.ToString()
            : notificationEvent.Key;

        if (!_rateLimiter.TryAcquire(key))
        {
            return false;
        }

        notification = new SystemNotification(
            notificationEvent.Title,
            notificationEvent.Message,
            notificationEvent.Kind,
            key);
        return true;
    }
}
