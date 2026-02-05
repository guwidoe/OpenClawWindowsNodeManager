using OpenClaw.Win.Core;
using Xunit;

namespace OpenClaw.Win.Core.Tests;

public class NodesStatusParserTests
{
    [Fact]
    public void ContainsNode_MatchesById()
    {
        var identity = new NodeIdentity { Id = "abc-123" };
        var text = "nodes: abc-123 connected";

        Assert.True(NodesStatusParser.ContainsNode(text, identity));
    }

    [Fact]
    public void ContainsNode_MatchesByDisplayName()
    {
        var identity = new NodeIdentity { DisplayName = "MYNODE" };
        var text = "connected nodes: mynode";

        Assert.True(NodesStatusParser.ContainsNode(text, identity));
    }

    [Fact]
    public void ContainsNode_ReturnsFalseWhenNoMatch()
    {
        var identity = new NodeIdentity { Id = "abc-123", DisplayName = "MYNODE" };
        var text = "connected nodes: other";

        Assert.False(NodesStatusParser.ContainsNode(text, identity));
    }

    [Fact]
    public void ContainsNode_ReturnsFalseForEmptyText()
    {
        var identity = new NodeIdentity { Id = "abc-123" };
        Assert.False(NodesStatusParser.ContainsNode("  ", identity));
    }
}
