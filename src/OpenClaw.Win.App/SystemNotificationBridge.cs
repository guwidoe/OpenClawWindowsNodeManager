using System;
using OpenClaw.Win.Core;

namespace OpenClaw.Win.App;

public interface ISystemNotificationPublisher
{
    void Publish(SystemNotification notification);
}

public sealed class TrayIconNotificationPublisher : ISystemNotificationPublisher
{
    private readonly TrayIconService _trayIcon;

    public TrayIconNotificationPublisher(TrayIconService trayIcon)
    {
        _trayIcon = trayIcon ?? throw new ArgumentNullException(nameof(trayIcon));
    }

    public void Publish(SystemNotification notification)
    {
        _trayIcon.ShowSystemNotification(notification.Title, notification.Message);
    }
}

public sealed class SystemNotificationBridge
{
    private readonly SystemNotificationRouter _router;
    private readonly ISystemNotificationPublisher _publisher;

    public SystemNotificationBridge(SystemNotificationRouter router, ISystemNotificationPublisher publisher)
    {
        _router = router ?? throw new ArgumentNullException(nameof(router));
        _publisher = publisher ?? throw new ArgumentNullException(nameof(publisher));
    }

    public void SetEnabled(bool enabled)
    {
        _router.Enabled = enabled;
    }

    public void Notify(SystemNotificationEvent notificationEvent)
    {
        if (_router.TryBuild(notificationEvent, out var notification))
        {
            _publisher.Publish(notification);
        }
    }
}
