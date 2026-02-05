using System;
using System.Threading;
using System.Threading.Tasks;

namespace OpenClaw.Win.Core;

public sealed class CanvasService
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private ICanvasRenderer? _renderer;
    private CanvasStatus _status = CanvasStatus.Uninitialized;

    public CanvasStatus Status => _status;
    public CanvasContent? LastContent { get; private set; }
    public DateTimeOffset? LastUpdatedAt { get; private set; }

    public event EventHandler<CanvasStatus>? StatusChanged;

    public void AttachRenderer(ICanvasRenderer renderer)
    {
        if (_renderer != null)
        {
            _renderer.StatusChanged -= OnRendererStatusChanged;
        }

        _renderer = renderer ?? throw new ArgumentNullException(nameof(renderer));
        _renderer.StatusChanged += OnRendererStatusChanged;
        UpdateStatus(_renderer.Status);
    }

    public async Task<bool> EnsureInitializedAsync(CancellationToken cancellationToken = default)
    {
        if (_renderer == null)
        {
            UpdateStatus(CanvasStatus.Uninitialized);
            return false;
        }

        await _renderer.InitializeAsync(cancellationToken).ConfigureAwait(false);
        UpdateStatus(_renderer.Status);
        return _renderer.Status == CanvasStatus.Ready;
    }

    public async Task RenderHtmlAsync(string html, string? title = null, CancellationToken cancellationToken = default)
    {
        var content = new CanvasContent(html, title ?? "Canvas");
        await RenderAsync(content, cancellationToken).ConfigureAwait(false);
    }

    public async Task RenderA2UiAsync(string payload, CancellationToken cancellationToken = default)
    {
        var content = A2UiRenderer.Render(payload);
        await RenderAsync(content, cancellationToken).ConfigureAwait(false);
    }

    public async Task RenderAsync(CanvasContent content, CancellationToken cancellationToken = default)
    {
        if (_renderer == null)
        {
            UpdateStatus(CanvasStatus.Uninitialized);
            return;
        }

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await _renderer.RenderAsync(content, cancellationToken).ConfigureAwait(false);
            LastContent = content;
            LastUpdatedAt = DateTimeOffset.UtcNow;
            UpdateStatus(_renderer.Status);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<CanvasEvalResult> EvalAsync(string script, CancellationToken cancellationToken = default)
    {
        if (_renderer == null)
        {
            UpdateStatus(CanvasStatus.Uninitialized);
            return new CanvasEvalResult(false, null, "Canvas renderer not available.");
        }

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var result = await _renderer.EvalAsync(script, cancellationToken).ConfigureAwait(false);
            UpdateStatus(_renderer.Status);
            return result;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<CanvasSnapshotResult> CaptureSnapshotAsync(CanvasSnapshotOptions? options = null, CancellationToken cancellationToken = default)
    {
        if (_renderer == null)
        {
            UpdateStatus(CanvasStatus.Uninitialized);
            return new CanvasSnapshotResult(false, null, "Canvas renderer not available.");
        }

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var result = await _renderer.CaptureSnapshotAsync(options, cancellationToken).ConfigureAwait(false);
            UpdateStatus(_renderer.Status);
            return result;
        }
        finally
        {
            _gate.Release();
        }
    }

    private void OnRendererStatusChanged(object? sender, CanvasStatus status)
    {
        UpdateStatus(status);
    }

    private void UpdateStatus(CanvasStatus status)
    {
        if (_status == status)
        {
            return;
        }

        _status = status;
        StatusChanged?.Invoke(this, status);
    }
}
