using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;
using OpenClaw.Win.Core;

namespace OpenClaw.Win.App;

public sealed class WebViewCanvasRenderer : ICanvasRenderer
{
    private readonly WebView2 _webView;
    private CanvasStatus _status = CanvasStatus.Uninitialized;
    private bool _initialized;

    public WebViewCanvasRenderer(WebView2 webView)
    {
        _webView = webView ?? throw new ArgumentNullException(nameof(webView));
    }

    public CanvasStatus Status => _status;

    public event EventHandler<CanvasStatus>? StatusChanged;

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        if (_initialized)
        {
            return;
        }

        if (!IsRuntimeAvailable())
        {
            UpdateStatus(CanvasStatus.MissingRuntime);
            return;
        }

        try
        {
            await RunOnUiAsync(() => _webView.EnsureCoreWebView2Async());
            _initialized = true;
            UpdateStatus(CanvasStatus.Ready);
        }
        catch
        {
            UpdateStatus(CanvasStatus.Error);
        }
    }

    public async Task RenderAsync(CanvasContent content, CancellationToken cancellationToken = default)
    {
        await InitializeAsync(cancellationToken).ConfigureAwait(false);
        if (_status != CanvasStatus.Ready)
        {
            return;
        }

        await RunOnUiAsync(() => _webView.NavigateToString(content.Html));
    }

    public async Task<CanvasEvalResult> EvalAsync(string script, CancellationToken cancellationToken = default)
    {
        await InitializeAsync(cancellationToken).ConfigureAwait(false);
        if (_status != CanvasStatus.Ready || _webView.CoreWebView2 == null)
        {
            return new CanvasEvalResult(false, null, "Canvas runtime not ready.");
        }

        try
        {
            var result = await RunOnUiAsync(() => _webView.ExecuteScriptAsync(script));
            return new CanvasEvalResult(true, NormalizeEvalResult(result), null);
        }
        catch (Exception ex)
        {
            return new CanvasEvalResult(false, null, ex.Message);
        }
    }

    public async Task<CanvasSnapshotResult> CaptureSnapshotAsync(CanvasSnapshotOptions? options = null, CancellationToken cancellationToken = default)
    {
        await InitializeAsync(cancellationToken).ConfigureAwait(false);
        if (_status != CanvasStatus.Ready || _webView.CoreWebView2 == null)
        {
            return new CanvasSnapshotResult(false, null, "Canvas runtime not ready.");
        }

        try
        {
            using var stream = new MemoryStream();
            await RunOnUiAsync(() => _webView.CoreWebView2.CapturePreviewAsync(CoreWebView2CapturePreviewImageFormat.Png, stream));
            return new CanvasSnapshotResult(true, stream.ToArray(), null);
        }
        catch (Exception ex)
        {
            return new CanvasSnapshotResult(false, null, ex.Message);
        }
    }

    private bool IsRuntimeAvailable()
    {
        try
        {
            _ = CoreWebView2Environment.GetAvailableBrowserVersionString();
            return true;
        }
        catch
        {
            return false;
        }
    }

    private Task RunOnUiAsync(Func<Task> action)
    {
        if (_webView.Dispatcher.CheckAccess())
        {
            return action();
        }

        return _webView.Dispatcher.InvokeAsync(action, DispatcherPriority.Normal).Task.Unwrap();
    }

    private Task RunOnUiAsync(Action action)
    {
        if (_webView.Dispatcher.CheckAccess())
        {
            action();
            return Task.CompletedTask;
        }

        return _webView.Dispatcher.InvokeAsync(action, DispatcherPriority.Normal).Task;
    }

    private Task<T> RunOnUiAsync<T>(Func<Task<T>> action)
    {
        if (_webView.Dispatcher.CheckAccess())
        {
            return action();
        }

        return _webView.Dispatcher.InvokeAsync(action, DispatcherPriority.Normal).Task.Unwrap();
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

    private static string? NormalizeEvalResult(string? result)
    {
        if (string.IsNullOrWhiteSpace(result))
        {
            return result;
        }

        return result.Trim('"');
    }
}
