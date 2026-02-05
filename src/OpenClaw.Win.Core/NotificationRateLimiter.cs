using System;
using System.Collections.Generic;

namespace OpenClaw.Win.Core;

public sealed class NotificationRateLimiter
{
    private readonly TimeSpan _cooldown;
    private readonly TimeSpan _globalCooldown;
    private readonly Func<DateTimeOffset> _now;
    private readonly Dictionary<string, DateTimeOffset> _lastByKey = new(StringComparer.OrdinalIgnoreCase);
    private DateTimeOffset _lastGlobal = DateTimeOffset.MinValue;

    public NotificationRateLimiter(
        TimeSpan cooldown,
        TimeSpan? globalCooldown = null,
        Func<DateTimeOffset>? now = null)
    {
        if (cooldown <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(cooldown));
        }

        _cooldown = cooldown;
        _globalCooldown = globalCooldown ?? cooldown;
        _now = now ?? (() => DateTimeOffset.UtcNow);
    }

    public bool TryAcquire(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return false;
        }

        var now = _now();
        if (now - _lastGlobal < _globalCooldown)
        {
            return false;
        }

        if (_lastByKey.TryGetValue(key, out var lastAt) && now - lastAt < _cooldown)
        {
            return false;
        }

        _lastByKey[key] = now;
        _lastGlobal = now;
        return true;
    }
}
