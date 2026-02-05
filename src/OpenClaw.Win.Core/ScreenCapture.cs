using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace OpenClaw.Win.Core;

public sealed record ScreenInfo(string Name, Rectangle Bounds, bool Primary);

public sealed record ScreenCaptureRequest(bool CaptureAllScreens, int? ScreenIndex = null);

public sealed record ScreenCaptureResult(bool Success, byte[]? PngData, string? Error, int Width, int Height);

public interface IScreenInfoProvider
{
    IReadOnlyList<ScreenInfo> GetScreens();
}

public interface IScreenImageProvider
{
    Bitmap Capture(Rectangle bounds);
}

public interface IScreenCaptureService
{
    Task<ScreenCaptureResult> CaptureAsync(ScreenCaptureRequest request, CancellationToken cancellationToken = default);
    IReadOnlyList<ScreenInfo> GetScreens();
}

public sealed class ScreenCaptureService : IScreenCaptureService
{
    private readonly IScreenInfoProvider _screenInfoProvider;
    private readonly IScreenImageProvider _screenImageProvider;

    public ScreenCaptureService(IScreenInfoProvider screenInfoProvider, IScreenImageProvider screenImageProvider)
    {
        _screenInfoProvider = screenInfoProvider ?? throw new ArgumentNullException(nameof(screenInfoProvider));
        _screenImageProvider = screenImageProvider ?? throw new ArgumentNullException(nameof(screenImageProvider));
    }

    public IReadOnlyList<ScreenInfo> GetScreens() => _screenInfoProvider.GetScreens();

    public Task<ScreenCaptureResult> CaptureAsync(ScreenCaptureRequest request, CancellationToken cancellationToken = default)
    {
        return Task.Run(() => Capture(request), cancellationToken);
    }

    private ScreenCaptureResult Capture(ScreenCaptureRequest request)
    {
        var screens = _screenInfoProvider.GetScreens();
        if (screens.Count == 0)
        {
            return new ScreenCaptureResult(false, null, "No screens detected.", 0, 0);
        }

        if (!request.CaptureAllScreens)
        {
            var index = request.ScreenIndex ?? GetPrimaryIndex(screens);
            if (index < 0 || index >= screens.Count)
            {
                return new ScreenCaptureResult(false, null, "Invalid screen index.", 0, 0);
            }

            var screen = screens[index];
            using var bitmap = _screenImageProvider.Capture(screen.Bounds);
            return Encode(bitmap);
        }

        var union = GetUnionBounds(screens);
        using var composite = new Bitmap(union.Width, union.Height);
        using (var graphics = Graphics.FromImage(composite))
        {
            graphics.Clear(Color.Black);

            foreach (var screen in screens)
            {
                using var bmp = _screenImageProvider.Capture(screen.Bounds);
                var offsetX = screen.Bounds.Left - union.Left;
                var offsetY = screen.Bounds.Top - union.Top;
                graphics.DrawImage(bmp, offsetX, offsetY, screen.Bounds.Width, screen.Bounds.Height);
            }
        }

        return Encode(composite);
    }

    private static int GetPrimaryIndex(IReadOnlyList<ScreenInfo> screens)
    {
        for (var i = 0; i < screens.Count; i++)
        {
            if (screens[i].Primary)
            {
                return i;
            }
        }

        return 0;
    }

    private static Rectangle GetUnionBounds(IEnumerable<ScreenInfo> screens)
    {
        var bounds = screens.Select(screen => screen.Bounds).ToArray();
        var left = bounds.Min(rect => rect.Left);
        var top = bounds.Min(rect => rect.Top);
        var right = bounds.Max(rect => rect.Right);
        var bottom = bounds.Max(rect => rect.Bottom);
        return Rectangle.FromLTRB(left, top, right, bottom);
    }

    private static ScreenCaptureResult Encode(Bitmap bitmap)
    {
        using var stream = new MemoryStream();
        bitmap.Save(stream, ImageFormat.Png);
        return new ScreenCaptureResult(true, stream.ToArray(), null, bitmap.Width, bitmap.Height);
    }
}

public sealed class DefaultScreenInfoProvider : IScreenInfoProvider
{
    public IReadOnlyList<ScreenInfo> GetScreens()
    {
        var screens = System.Windows.Forms.Screen.AllScreens;
        var list = new List<ScreenInfo>(screens.Length);
        foreach (var screen in screens)
        {
            list.Add(new ScreenInfo(screen.DeviceName, screen.Bounds, screen.Primary));
        }

        return list;
    }
}

public sealed class DefaultScreenImageProvider : IScreenImageProvider
{
    public Bitmap Capture(Rectangle bounds)
    {
        var bitmap = new Bitmap(bounds.Width, bounds.Height);
        using var graphics = Graphics.FromImage(bitmap);
        graphics.CopyFromScreen(bounds.Left, bounds.Top, 0, 0, bounds.Size);
        return bitmap;
    }
}
