using OpenClaw.Win.Core;
using Xunit;

namespace OpenClaw.Win.Core.Tests;

public class NodeStatusTests
{
    [Fact]
    public void ToConnectionState_ConnectedWhenRunningAndConnected()
    {
        var status = new NodeStatus { IsRunning = true, IsConnected = true };
        Assert.Equal(NodeConnectionState.Connected, status.ToConnectionState());
    }

    [Fact]
    public void ToConnectionState_DegradedWhenRunningNotConnected()
    {
        var status = new NodeStatus { IsRunning = true, IsConnected = false };
        Assert.Equal(NodeConnectionState.Degraded, status.ToConnectionState());
    }

    [Fact]
    public void ToConnectionState_DisconnectedWhenNotRunning()
    {
        var status = new NodeStatus { IsRunning = false };
        Assert.Equal(NodeConnectionState.Disconnected, status.ToConnectionState());
    }

    [Fact]
    public void ToConnectionState_ErrorWhenIssueNotTokenMissing()
    {
        var status = new NodeStatus { Issue = NodeIssue.TokenInvalid };
        Assert.Equal(NodeConnectionState.Error, status.ToConnectionState());
    }

    [Fact]
    public void ToConnectionState_DisconnectedWhenTokenMissingAndNotRunning()
    {
        var status = new NodeStatus { Issue = NodeIssue.TokenMissing, IsRunning = false };
        Assert.Equal(NodeConnectionState.Disconnected, status.ToConnectionState());
    }
}
