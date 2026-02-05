using System;
using System.IO;
using System.Threading.Tasks;
using OpenClaw.Win.Core;
using Xunit;

namespace OpenClaw.Win.Core.Tests;

[Collection("StateDir")]
public class ScreenRecorderTests
{
    [Fact]
    public async Task Recorder_WritesFrames_OnTicks()
    {
        using var temp = new TempStateDir();
        var capture = new FakeCaptureService();
        var scheduler = new ManualScheduler();
        var recorder = new ScreenRecorder(capture, scheduler);

        var session = await recorder.StartAsync(new ScreenRecordingOptions(TimeSpan.FromSeconds(1), CaptureAllScreens: true));

        await scheduler.TickAsync();
        await scheduler.TickAsync();

        Assert.Equal(2, session.FramesCaptured);
        Assert.True(Directory.Exists(session.Directory));
        Assert.Equal(2, Directory.GetFiles(session.Directory, "frame-*.png").Length);

        await recorder.StopAsync();
    }

    private sealed class FakeCaptureService : IScreenCaptureService
    {
        public Task<ScreenCaptureResult> CaptureAsync(ScreenCaptureRequest request, System.Threading.CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new ScreenCaptureResult(true, new byte[] { 1, 2, 3 }, null, 1, 1));
        }

        public System.Collections.Generic.IReadOnlyList<ScreenInfo> GetScreens()
        {
            return Array.Empty<ScreenInfo>();
        }
    }

    private sealed class ManualScheduler : IRecordingScheduler
    {
        private Func<Task>? _tick;

        public void Start(TimeSpan interval, Func<Task> tickAsync)
        {
            _tick = tickAsync;
        }

        public void Stop()
        {
            _tick = null;
        }

        public Task TickAsync()
        {
            return _tick?.Invoke() ?? Task.CompletedTask;
        }
    }
}
