using OpenClaw.Win.Core;
using Xunit;

namespace OpenClaw.Win.Core.Tests;

public class StatusStabilizerTests
{
    [Fact]
    public void Stabilize_FirstStatusBecomesStable()
    {
        var stabilizer = new StatusStabilizer();
        var first = new NodeStatus { IsRunning = false };

        var stable = stabilizer.Stabilize(first);

        Assert.Same(first, stable);
    }

    [Fact]
    public void Stabilize_RequiresTwoConsecutiveStates()
    {
        var stabilizer = new StatusStabilizer();
        var stable = stabilizer.Stabilize(new NodeStatus { IsRunning = false });

        var candidate = new NodeStatus { IsRunning = true, IsConnected = true };
        var stillStable = stabilizer.Stabilize(candidate);

        Assert.Same(stable, stillStable);

        var promoted = stabilizer.Stabilize(candidate);

        Assert.Same(candidate, promoted);
    }

    [Fact]
    public void Stabilize_ResetsPendingWhenStateReturns()
    {
        var stabilizer = new StatusStabilizer();
        _ = stabilizer.Stabilize(new NodeStatus { IsRunning = false });

        _ = stabilizer.Stabilize(new NodeStatus { IsRunning = true, IsConnected = false });

        var same = stabilizer.Stabilize(new NodeStatus { IsRunning = false });

        Assert.Equal(NodeConnectionState.Disconnected, same.ToConnectionState());

        var candidate = new NodeStatus { IsRunning = true, IsConnected = true };
        var stillStable = stabilizer.Stabilize(candidate);
        Assert.Equal(NodeConnectionState.Disconnected, stillStable.ToConnectionState());
    }
}
