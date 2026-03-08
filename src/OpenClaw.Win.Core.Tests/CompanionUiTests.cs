using OpenClaw.Win.Core;
using Xunit;

namespace OpenClaw.Win.Core.Tests;

public class CompanionUiTests
{
    [Theory]
    [InlineData("connection", CompanionTab.Connection)]
    [InlineData("settings", CompanionTab.Connection)]
    [InlineData("node-host", CompanionTab.NodeHost)]
    [InlineData("relay", CompanionTab.ChromeRelay)]
    [InlineData("logs", CompanionTab.Logs)]
    public void TryParse_AcceptsKnownAliases(string input, CompanionTab expected)
    {
        var ok = CompanionTabNames.TryParse(input, out var actual);

        Assert.True(ok);
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void ParseAppArguments_TabImpliesShowSettings()
    {
        var request = CompanionUiLaunchRequest.ParseAppArguments(new[] { "--tab", "approvals" });

        Assert.True(request.ShowSettings);
        Assert.Equal(CompanionTab.Approvals, request.Tab);
    }

    [Fact]
    public void ToAppArguments_UsesCanonicalTabName()
    {
        var request = CompanionUiLaunchRequest.Show(CompanionTab.ChromeRelay);

        Assert.Equal(new[] { "--show-settings", "--tab", "chrome-relay" }, request.ToAppArguments());
    }
}
