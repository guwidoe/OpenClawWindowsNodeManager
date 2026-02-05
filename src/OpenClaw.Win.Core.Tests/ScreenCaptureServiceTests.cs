using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Threading.Tasks;
using OpenClaw.Win.Core;
using Xunit;

namespace OpenClaw.Win.Core.Tests;

public class ScreenCaptureServiceTests
{
    [Fact]
    public async Task CaptureAsync_AllScreens_CombinesBounds()
    {
        var screens = new List<ScreenInfo>
        {
            new("Screen1", new Rectangle(0, 0, 800, 600), true),
            new("Screen2", new Rectangle(800, 0, 1024, 768), false)
        };

        var service = new ScreenCaptureService(new FakeScreenInfoProvider(screens), new FakeScreenImageProvider());
        var result = await service.CaptureAsync(new ScreenCaptureRequest(CaptureAllScreens: true));

        Assert.True(result.Success);
        Assert.Equal(1824, result.Width);
        Assert.Equal(768, result.Height);

        using var stream = new MemoryStream(result.PngData!);
        using var image = Image.FromStream(stream);
        Assert.Equal(1824, image.Width);
        Assert.Equal(768, image.Height);
    }

    [Fact]
    public async Task CaptureAsync_PrimaryScreen_UsesPrimary()
    {
        var screens = new List<ScreenInfo>
        {
            new("Screen1", new Rectangle(0, 0, 800, 600), false),
            new("Screen2", new Rectangle(800, 0, 1024, 768), true)
        };

        var service = new ScreenCaptureService(new FakeScreenInfoProvider(screens), new FakeScreenImageProvider());
        var result = await service.CaptureAsync(new ScreenCaptureRequest(CaptureAllScreens: false));

        Assert.True(result.Success);
        Assert.Equal(1024, result.Width);
        Assert.Equal(768, result.Height);
    }

    private sealed class FakeScreenInfoProvider : IScreenInfoProvider
    {
        private readonly IReadOnlyList<ScreenInfo> _screens;

        public FakeScreenInfoProvider(IReadOnlyList<ScreenInfo> screens)
        {
            _screens = screens;
        }

        public IReadOnlyList<ScreenInfo> GetScreens() => _screens;
    }

    private sealed class FakeScreenImageProvider : IScreenImageProvider
    {
        public Bitmap Capture(Rectangle bounds)
        {
            return new Bitmap(bounds.Width, bounds.Height);
        }
    }
}
