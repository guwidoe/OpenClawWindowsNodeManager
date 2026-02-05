using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace OpenClaw.Win.Core;

public sealed record ScreenRecordingOptions(
    TimeSpan Interval,
    bool CaptureAllScreens,
    int? ScreenIndex = null,
    string? OutputDirectory = null);

public sealed class ScreenRecordingSession
{
    public string Directory { get; }
    public DateTimeOffset StartedAt { get; }
    public DateTimeOffset? StoppedAt { get; internal set; }
    public int FramesCaptured { get; internal set; }
    public string? LastError { get; internal set; }

    public ScreenRecordingSession(string directory, DateTimeOffset startedAt)
    {
        Directory = directory;
        StartedAt = startedAt;
    }
}

public interface IRecordingScheduler
{
    void Start(TimeSpan interval, Func<Task> tickAsync);
    void Stop();
}

public sealed class TimerRecordingScheduler : IRecordingScheduler, IDisposable
{
    private System.Threading.Timer? _timer;
    private Func<Task>? _tickAsync;

    public void Start(TimeSpan interval, Func<Task> tickAsync)
    {
        _tickAsync = tickAsync;
        _timer?.Dispose();
        _timer = new System.Threading.Timer(async _ => await TickAsync().ConfigureAwait(false), null, interval, interval);
    }

    public void Stop()
    {
        _timer?.Dispose();
        _timer = null;
        _tickAsync = null;
    }

    private async Task TickAsync()
    {
        var tick = _tickAsync;
        if (tick != null)
        {
            await tick().ConfigureAwait(false);
        }
    }

    public void Dispose()
    {
        Stop();
    }
}

public sealed class ScreenRecorder
{
    private readonly IScreenCaptureService _captureService;
    private readonly IRecordingScheduler _scheduler;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly SemaphoreSlim _captureLock = new(1, 1);
    private ScreenRecordingSession? _session;
    private ScreenRecordingOptions? _options;
    private int _frameIndex;

    public ScreenRecorder(IScreenCaptureService captureService, IRecordingScheduler? scheduler = null)
    {
        _captureService = captureService ?? throw new ArgumentNullException(nameof(captureService));
        _scheduler = scheduler ?? new TimerRecordingScheduler();
    }

    public bool IsRecording => _session != null;
    public ScreenRecordingSession? CurrentSession => _session;

    public event EventHandler<ScreenRecordingSession?>? RecordingStateChanged;

    public async Task<ScreenRecordingSession> StartAsync(ScreenRecordingOptions options, CancellationToken cancellationToken = default)
    {
        if (options.Interval <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(options.Interval));
        }

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_session != null)
            {
                return _session;
            }

            var directory = options.OutputDirectory;
            if (string.IsNullOrWhiteSpace(directory))
            {
                AppPaths.EnsureDirectories();
                var timestamp = DateTimeOffset.Now.ToString("yyyyMMdd-HHmmss");
                directory = Path.Combine(AppPaths.DiagnosticsDir, $"recording-{timestamp}");
            }

            Directory.CreateDirectory(directory);
            _options = options with { OutputDirectory = directory };
            _frameIndex = 0;
            _session = new ScreenRecordingSession(directory, DateTimeOffset.UtcNow);
            _scheduler.Start(options.Interval, CaptureFrameAsync);
            RecordingStateChanged?.Invoke(this, _session);
            return _session;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<ScreenRecordingSession?> StopAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_session == null)
            {
                return null;
            }

            _scheduler.Stop();
            _session.StoppedAt = DateTimeOffset.UtcNow;
            RecordingStateChanged?.Invoke(this, _session);
            var completed = _session;
            _session = null;
            _options = null;
            return completed;
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task CaptureFrameAsync()
    {
        if (_session == null || _options == null)
        {
            return;
        }

        if (!await _captureLock.WaitAsync(0).ConfigureAwait(false))
        {
            return;
        }

        try
        {
            var request = new ScreenCaptureRequest(_options.CaptureAllScreens, _options.ScreenIndex);
            var result = await _captureService.CaptureAsync(request).ConfigureAwait(false);
            if (!result.Success || result.PngData == null)
            {
                _session.LastError = result.Error ?? "Capture failed.";
                RecordingStateChanged?.Invoke(this, _session);
                return;
            }

            var path = Path.Combine(_session.Directory, $"frame-{_frameIndex:0000}.png");
            await File.WriteAllBytesAsync(path, result.PngData).ConfigureAwait(false);
            _frameIndex++;
            _session.FramesCaptured = _frameIndex;
            RecordingStateChanged?.Invoke(this, _session);
        }
        catch (Exception ex)
        {
            _session.LastError = ex.Message;
            RecordingStateChanged?.Invoke(this, _session);
        }
        finally
        {
            _captureLock.Release();
        }
    }
}
