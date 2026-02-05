using OpenClaw.Win.Cli;
using OpenClaw.Win.Core;
using Xunit;

namespace OpenClaw.Win.Cli.Tests;

public class CliStatusFormatterTests
{
    [Fact]
    public void MapExitCode_Connected_ReturnsSuccess()
    {
        var status = new NodeStatus { IsRunning = true, IsConnected = true };
        Assert.Equal(ExitCodes.Success, CliStatusFormatter.MapExitCode(status));
    }

    [Fact]
    public void MapExitCode_Disconnected_ReturnsDisconnected()
    {
        var status = new NodeStatus { IsRunning = false };
        Assert.Equal(ExitCodes.Disconnected, CliStatusFormatter.MapExitCode(status));
    }

    [Fact]
    public void MapExitCode_Degraded_ReturnsDegraded()
    {
        var status = new NodeStatus { IsRunning = true, IsConnected = false };
        Assert.Equal(ExitCodes.Degraded, CliStatusFormatter.MapExitCode(status));
    }

    [Fact]
    public void MapExitCode_IssuesOverrideStatus()
    {
        var status = new NodeStatus { Issue = NodeIssue.OpenClawMissing };
        Assert.Equal(ExitCodes.OpenClawMissing, CliStatusFormatter.MapExitCode(status));

        status.Issue = NodeIssue.ConfigMissing;
        Assert.Equal(ExitCodes.ConfigMissing, CliStatusFormatter.MapExitCode(status));

        status.Issue = NodeIssue.TokenInvalid;
        Assert.Equal(ExitCodes.AuthTokenError, CliStatusFormatter.MapExitCode(status));

        status.Issue = NodeIssue.PairingRequired;
        Assert.Equal(ExitCodes.PairingRequired, CliStatusFormatter.MapExitCode(status));
    }

    [Fact]
    public void FormatStatus_IncludesGatewayAndError()
    {
        var status = new NodeStatus
        {
            IsRunning = true,
            IsConnected = false,
            GatewayHost = "gw.local",
            GatewayPort = 443,
            LastError = "oops"
        };

        var output = CliStatusFormatter.FormatStatus(status);
        Assert.Contains("gateway: gw.local:443", output);
        Assert.Contains("error: oops", output);
        Assert.Contains("state: Degraded", output);
    }
}
