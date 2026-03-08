using OpenClaw.Win.Cli;
using OpenClaw.Win.Core;
using Xunit;

namespace OpenClaw.Win.Cli.Tests;

public class UiCommandParserTests
{
    [Fact]
    public void TryParse_WithoutArgs_DefaultsToShowSettings()
    {
        var ok = UiCommandParser.TryParse([], out var options, out var error);

        Assert.True(ok);
        Assert.Null(error);
        Assert.False(options.ListTabs);
        Assert.True(options.LaunchRequest.ShowSettings);
        Assert.Null(options.LaunchRequest.Tab);
    }

    [Fact]
    public void TryParse_ShowWithTab_UsesParsedTab()
    {
        var ok = UiCommandParser.TryParse(["show", "--tab", "relay"], out var options, out var error);

        Assert.True(ok);
        Assert.Null(error);
        Assert.Equal(CompanionTab.ChromeRelay, options.LaunchRequest.Tab);
    }

    [Fact]
    public void TryParse_Tabs_ReturnsListMode()
    {
        var ok = UiCommandParser.TryParse(["tabs"], out var options, out var error);

        Assert.True(ok);
        Assert.Null(error);
        Assert.True(options.ListTabs);
    }

    [Fact]
    public void TryParse_InvalidTab_ReturnsHelpfulError()
    {
        var ok = UiCommandParser.TryParse(["show", "--tab", "wat"], out _, out var error);

        Assert.False(ok);
        Assert.Contains("Supported tabs", error);
    }
}
