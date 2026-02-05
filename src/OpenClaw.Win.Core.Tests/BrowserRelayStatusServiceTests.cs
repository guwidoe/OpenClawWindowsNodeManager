using System.Threading;
using System.Threading.Tasks;
using OpenClaw.Win.Core;
using Xunit;

namespace OpenClaw.Win.Core.Tests;

public class BrowserRelayStatusServiceTests
{
    [Fact]
    public async Task CheckAsync_NotConfigured_ReturnsNotConfigured()
    {
        var service = new BrowserRelayStatusService(new FakeRelayService(false));
        var status = await service.CheckAsync(0);

        Assert.Equal(BrowserRelayState.NotConfigured, status.State);
    }

    [Fact]
    public async Task CheckAsync_Connected_ReturnsConnected()
    {
        var service = new BrowserRelayStatusService(new FakeRelayService(true));
        var status = await service.CheckAsync(18792);

        Assert.Equal(BrowserRelayState.Connected, status.State);
    }

    private sealed class FakeRelayService : ChromeRelayService
    {
        private readonly bool _result;

        public FakeRelayService(bool result)
        {
            _result = result;
        }

        public override Task<bool> VerifyRelayAsync(int port, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_result);
        }
    }
}
