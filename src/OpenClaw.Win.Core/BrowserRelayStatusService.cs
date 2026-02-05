using System;
using System.Threading;
using System.Threading.Tasks;

namespace OpenClaw.Win.Core;

public enum BrowserRelayState
{
    Unknown,
    NotConfigured,
    Connected,
    Disconnected,
    Error
}

public sealed record BrowserRelayStatus(
    BrowserRelayState State,
    string Title,
    string Message,
    DateTimeOffset CheckedAt);

public sealed class BrowserRelayStatusService
{
    private readonly ChromeRelayService _relayService;

    public BrowserRelayStatusService(ChromeRelayService relayService)
    {
        _relayService = relayService ?? throw new ArgumentNullException(nameof(relayService));
    }

    public async Task<BrowserRelayStatus> CheckAsync(int port, CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.Now;
        if (port <= 0)
        {
            return new BrowserRelayStatus(
                BrowserRelayState.NotConfigured,
                "Relay not configured",
                "Set a relay port and click Verify.",
                now);
        }

        try
        {
            var ok = await _relayService.VerifyRelayAsync(port, cancellationToken).ConfigureAwait(false);
            if (ok)
            {
                return new BrowserRelayStatus(
                    BrowserRelayState.Connected,
                    "Relay connected",
                    "Extension reachable. Keep a Chrome tab open with the extension active.",
                    now);
            }

            return new BrowserRelayStatus(
                BrowserRelayState.Disconnected,
                "Relay disconnected",
                "Relay not reachable. Enable the OpenClaw extension and keep a tab open.",
                now);
        }
        catch (Exception ex)
        {
            return new BrowserRelayStatus(
                BrowserRelayState.Error,
                "Relay check failed",
                ex.Message,
                now);
        }
    }
}
