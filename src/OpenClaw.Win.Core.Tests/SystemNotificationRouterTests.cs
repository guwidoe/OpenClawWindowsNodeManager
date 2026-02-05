using System;
using OpenClaw.Win.Core;
using Xunit;

namespace OpenClaw.Win.Core.Tests;

public class SystemNotificationRouterTests
{
    [Fact]
    public void Router_Disabled_SuppressesNotifications()
    {
        var now = DateTimeOffset.UtcNow;
        var limiter = new NotificationRateLimiter(TimeSpan.FromSeconds(30), now: () => now);
        var router = new SystemNotificationRouter(limiter) { Enabled = false };

        var ok = router.TryBuild(
            new SystemNotificationEvent(SystemNotificationKind.StatusChange, "OpenClaw", "Connected"),
            out _);

        Assert.False(ok);
    }

    [Fact]
    public void Router_RateLimitsRepeatedNotifications()
    {
        var now = DateTimeOffset.UtcNow;
        var limiter = new NotificationRateLimiter(TimeSpan.FromSeconds(30), now: () => now);
        var router = new SystemNotificationRouter(limiter) { Enabled = true };

        var first = router.TryBuild(
            new SystemNotificationEvent(SystemNotificationKind.StatusChange, "OpenClaw", "Connected"),
            out _);

        var second = router.TryBuild(
            new SystemNotificationEvent(SystemNotificationKind.StatusChange, "OpenClaw", "Connected"),
            out _);

        Assert.True(first);
        Assert.False(second);
    }
}
