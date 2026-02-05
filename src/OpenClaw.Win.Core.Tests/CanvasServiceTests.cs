using System.Threading.Tasks;
using OpenClaw.Win.Core;
using Xunit;

namespace OpenClaw.Win.Core.Tests;

public class CanvasServiceTests
{
    [Fact]
    public async Task RenderAsync_StoresContentAndUpdatesTimestamp()
    {
        var service = new CanvasService();
        var renderer = new FakeCanvasRenderer();
        service.AttachRenderer(renderer);

        await service.RenderHtmlAsync("<html></html>", "Test");

        Assert.NotNull(service.LastContent);
        Assert.Equal("Test", service.LastContent?.Title);
        Assert.NotNull(service.LastUpdatedAt);
    }

    [Fact]
    public async Task EvalAsync_WithoutRenderer_ReturnsError()
    {
        var service = new CanvasService();
        var result = await service.EvalAsync("1+1");

        Assert.False(result.Success);
        Assert.Equal("Canvas renderer not available.", result.Error);
    }

    [Fact]
    public async Task CaptureSnapshotAsync_UsesRenderer()
    {
        var service = new CanvasService();
        var renderer = new FakeCanvasRenderer();
        service.AttachRenderer(renderer);

        var result = await service.CaptureSnapshotAsync();

        Assert.True(result.Success);
    }
}
