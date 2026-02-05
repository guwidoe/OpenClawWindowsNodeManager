using System;
using OpenClaw.Win.Core;
using Xunit;

namespace OpenClaw.Win.Core.Tests;

public class NodeStatusParserTests
{
    [Fact]
    public void Parse_JsonWithNestedServiceFields_PopulatesStatus()
    {
        var json = """
                   {
                     "service": {
                       "loaded": true,
                       "runtime": {
                         "status": "stopped",
                         "state": "Ready"
                       }
                     },
                     "gateway": {
                       "host": "openclaw.example",
                       "port": 443
                     },
                     "nodeId": "node-123",
                     "displayName": "MyNode",
                     "lastConnectedAt": "2026-02-05T18:00:00Z"
                   }
                   """;

        var status = NodeStatusParser.Parse(json, null);

        Assert.True(status.IsInstalled);
        Assert.False(status.IsRunning);
        Assert.Equal("openclaw.example", status.GatewayHost);
        Assert.Equal(443, status.GatewayPort);
        Assert.Equal("node-123", status.NodeId);
        Assert.Equal("MyNode", status.DisplayName);
        Assert.Equal(DateTimeOffset.Parse("2026-02-05T18:00:00Z"), status.LastConnectedAt);
    }

    [Fact]
    public void Parse_JsonWithRunningConnected_PopulatesFlags()
    {
        var json = """
                   {
                     "running": true,
                     "connected": true
                   }
                   """;

        var status = NodeStatusParser.Parse(json, null);

        Assert.True(status.IsRunning);
        Assert.True(status.IsConnected);
    }

    [Fact]
    public void Parse_TextHeuristics_DetectsPairingAndTokenIssues()
    {
        var text = "Pairing required: approve this device.";
        var status = NodeStatusParser.Parse(null, text);

        Assert.Equal(NodeIssue.PairingRequired, status.Issue);
        Assert.Equal("Pairing required.", status.LastError);
    }

    [Fact]
    public void Parse_TextHeuristics_PairingOverridesTokenError()
    {
        var text = "Unauthorized token. Pairing required.";
        var status = NodeStatusParser.Parse(null, text);

        Assert.Equal(NodeIssue.PairingRequired, status.Issue);
        Assert.Equal("Authentication failed.", status.LastError);
    }

    [Fact]
    public void Parse_TextHeuristics_DetectsGatewayUnreachable()
    {
        var text = "connect failed: ENOTFOUND gateway host";
        var status = NodeStatusParser.Parse(null, text);

        Assert.Equal(NodeIssue.GatewayUnreachable, status.Issue);
        Assert.Equal("Gateway unreachable.", status.LastError);
    }

    [Fact]
    public void Parse_JsonWithStringValues_ParsesTypes()
    {
        var json = """
                   {
                     "running": "true",
                     "connected": "false",
                     "gatewayHost": "gw.local",
                     "gatewayPort": "443"
                   }
                   """;

        var status = NodeStatusParser.Parse(json, null);

        Assert.True(status.IsRunning);
        Assert.False(status.IsConnected);
        Assert.Equal("gw.local", status.GatewayHost);
        Assert.Equal(443, status.GatewayPort);
    }
}
