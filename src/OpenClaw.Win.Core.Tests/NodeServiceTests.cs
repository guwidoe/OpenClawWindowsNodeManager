using System;
using System.Linq;
using System.Threading.Tasks;
using OpenClaw.Win.Core;
using Xunit;

namespace OpenClaw.Win.Core.Tests;

public class NodeServiceTests
{
    private static ProcessResult StatusResult(string json) => new()
    {
        ExitCode = 0,
        StdOut = json
    };

    [Fact]
    public async Task ConnectAsync_CaptureEnabled_StartsHiddenRunner()
    {
        using var temp = new TempStateDir();
        var configStore = new FakeConfigStore
        {
            Config = new AppConfig
            {
                GatewayHost = "gw.local",
                GatewayPort = 443,
                UseTls = true,
                DisplayName = "NODE-A",
                CaptureNodeHostOutput = true
            }
        };

        var tokenStore = new FakeTokenStore { Token = "token-123" };
        var cliLocator = new FakeCliLocator();
        var processRunner = new FakeProcessRunner();
        processRunner.Results["node status --json"] = StatusResult("""
                                                                  {
                                                                    "service": { "loaded": true },
                                                                    "runtime": { "status": "stopped" }
                                                                  }
                                                                  """);

        var nodeProcessManager = new FakeNodeProcessManager();
        var nodeHostRunner = new FakeNodeHostRunner();

        var service = new NodeService(configStore, tokenStore, cliLocator, processRunner, nodeProcessManager, nodeHostRunner);
        await service.ConnectAsync(TimeSpan.Zero);

        Assert.Equal(1, nodeHostRunner.StartCalls);
        Assert.Equal("node run", nodeHostRunner.LastRunArgs);
        Assert.Equal("token-123", nodeHostRunner.LastToken);
    }

    [Fact]
    public async Task ConnectAsync_CaptureEnabled_StopsExistingNodeHost()
    {
        using var temp = new TempStateDir();
        var configStore = new FakeConfigStore
        {
            Config = new AppConfig
            {
                GatewayHost = "gw.local",
                GatewayPort = 443,
                UseTls = true,
                DisplayName = "NODE-A",
                CaptureNodeHostOutput = true
            }
        };

        var processRunner = new FakeProcessRunner();
        processRunner.Results["node status --json"] = StatusResult("""
                                                                  {
                                                                    "service": { "loaded": true },
                                                                    "running": true,
                                                                    "connected": true
                                                                  }
                                                                  """);

        var nodeProcessManager = new FakeNodeProcessManager
        {
            ForegroundIds = new[] { 101 }
        };

        var nodeHostRunner = new FakeNodeHostRunner();

        var service = new NodeService(
            configStore,
            new FakeTokenStore(),
            new FakeCliLocator(),
            processRunner,
            nodeProcessManager,
            nodeHostRunner);

        await service.ConnectAsync(TimeSpan.Zero);

        Assert.Contains(processRunner.Calls, call => call.Arguments == "node stop --json");
        Assert.True(nodeProcessManager.KillCalls > 0);
        Assert.Equal(1, nodeHostRunner.StartCalls);
    }

    [Fact]
    public async Task ConnectAsync_CaptureEnabled_MissingHost_ReturnsConfigMissing()
    {
        using var temp = new TempStateDir();
        var configStore = new FakeConfigStore
        {
            Config = new AppConfig
            {
                GatewayHost = "",
                CaptureNodeHostOutput = true
            }
        };

        var processRunner = new FakeProcessRunner();
        processRunner.Results["node status --json"] = StatusResult("""
                                                                  {
                                                                    "service": { "loaded": true },
                                                                    "runtime": { "status": "stopped" }
                                                                  }
                                                                  """);

        var service = new NodeService(
            configStore,
            new FakeTokenStore(),
            new FakeCliLocator(),
            processRunner,
            new FakeNodeProcessManager(),
            new FakeNodeHostRunner());

        var status = await service.ConnectAsync(TimeSpan.Zero);

        Assert.Equal(NodeIssue.ConfigMissing, status.Issue);
    }

    [Fact]
    public async Task DisconnectAsync_StopsHiddenRunner()
    {
        using var temp = new TempStateDir();
        var nodeHostRunner = new FakeNodeHostRunner { IsRunning = true };
        var processRunner = new FakeProcessRunner();
        processRunner.Results["node stop --json"] = new ProcessResult { ExitCode = 0 };

        var service = new NodeService(
            new FakeConfigStore(),
            new FakeTokenStore(),
            new FakeCliLocator(),
            processRunner,
            new FakeNodeProcessManager(),
            nodeHostRunner);

        await service.DisconnectAsync(TimeSpan.Zero);

        Assert.Equal(1, nodeHostRunner.StopCalls);
    }

    [Fact]
    public async Task GetStatusAsync_FailedStatusCheck_IsMarkedTransient()
    {
        using var temp = new TempStateDir();
        var configStore = new FakeConfigStore
        {
            Config = new AppConfig { GatewayHost = "gw.local" }
        };

        var tokenStore = new FakeTokenStore { Token = "token" };
        var processRunner = new FakeProcessRunner();
        processRunner.Results["node status --json"] = new ProcessResult
        {
            ExitCode = 1,
            StdErr = "boom"
        };

        var service = new NodeService(
            configStore,
            tokenStore,
            new FakeCliLocator(),
            processRunner,
            new FakeNodeProcessManager(),
            new FakeNodeHostRunner());

        var status = await service.GetStatusAsync();

        Assert.True(status.IsStatusCheckFailed);
        Assert.Equal(NodeIssue.None, status.Issue);
    }

    [Fact]
    public async Task GetStatusAsync_ErrorOutputStillSurfacesIssues()
    {
        using var temp = new TempStateDir();
        var configStore = new FakeConfigStore
        {
            Config = new AppConfig { GatewayHost = "gw.local" }
        };

        var processRunner = new FakeProcessRunner();
        processRunner.Results["node status --json"] = new ProcessResult
        {
            ExitCode = 1,
            StdErr = "token invalid"
        };

        var service = new NodeService(
            configStore,
            new FakeTokenStore { Token = "token" },
            new FakeCliLocator(),
            processRunner,
            new FakeNodeProcessManager(),
            new FakeNodeHostRunner());

        var status = await service.GetStatusAsync();

        Assert.False(status.IsStatusCheckFailed);
        Assert.Equal(NodeIssue.TokenInvalid, status.Issue);
    }
}
