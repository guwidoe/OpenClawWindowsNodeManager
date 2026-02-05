using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using OpenClaw.Win.Core;
using WpfApplication = System.Windows.Application;
using WpfMessageBox = System.Windows.MessageBox;

namespace OpenClaw.Win.App;

public partial class MainWindow
{
    private const string WebView2DownloadUrl = "https://go.microsoft.com/fwlink/p/?LinkId=2124703";
    private WebViewCanvasRenderer? _canvasRenderer;
    private string? _lastCanvasSnapshotPath;
    private string? _lastScreenCapturePath;
    private CaptureIndicatorWindow? _captureIndicator;

    private async Task InitializeCanvasAsync()
    {
        var app = (App)WpfApplication.Current;
        if (_canvasRenderer != null)
        {
            return;
        }

        _canvasRenderer = new WebViewCanvasRenderer(CanvasWebView);
        app.CanvasService.AttachRenderer(_canvasRenderer);
        app.CanvasService.StatusChanged += (_, status) => Dispatcher.Invoke(() => UpdateCanvasStatus(status));

        await app.CanvasService.EnsureInitializedAsync();
        UpdateCanvasStatus(app.CanvasService.Status);

        if (app.CanvasService.Status == CanvasStatus.Ready && app.CanvasService.LastContent == null)
        {
            await app.CanvasService.RenderA2UiAsync(string.Empty);
        }

        PopulateScreens();
        app.ScreenRecorder.RecordingStateChanged += (_, session) => Dispatcher.Invoke(() => UpdateRecordingStatus(session));
        UpdateRecordingButtons();
    }

    private void UpdateCanvasStatus(CanvasStatus status)
    {
        CanvasInstallButton.Visibility = Visibility.Collapsed;
        CanvasOverlayText.Visibility = Visibility.Collapsed;

        switch (status)
        {
            case CanvasStatus.Ready:
                CanvasStatusText.Text = "Canvas ready.";
                CanvasStatusDetails.Text = "Agent UI will render here when available.";
                CanvasOverlayText.Visibility = Visibility.Collapsed;
                break;
            case CanvasStatus.MissingRuntime:
                CanvasStatusText.Text = "WebView2 runtime missing.";
                CanvasStatusDetails.Text = "Install the WebView2 runtime to render the canvas.";
                CanvasInstallButton.Visibility = Visibility.Visible;
                CanvasOverlayText.Text = "WebView2 runtime missing.";
                CanvasOverlayText.Visibility = Visibility.Visible;
                break;
            case CanvasStatus.Error:
                CanvasStatusText.Text = "Canvas error.";
                CanvasStatusDetails.Text = "Failed to initialize the embedded canvas.";
                CanvasOverlayText.Text = "Canvas failed to load.";
                CanvasOverlayText.Visibility = Visibility.Visible;
                break;
            default:
                CanvasStatusText.Text = "Canvas not initialized.";
                CanvasStatusDetails.Text = "Open this tab to load the embedded canvas.";
                CanvasOverlayText.Text = "Canvas will appear here.";
                CanvasOverlayText.Visibility = Visibility.Visible;
                break;
        }
    }

    private async void CanvasReloadButton_Click(object sender, RoutedEventArgs e)
    {
        var app = (App)WpfApplication.Current;
        if (app.CanvasService.LastContent != null)
        {
            await app.CanvasService.RenderAsync(app.CanvasService.LastContent);
        }
        else
        {
            await app.CanvasService.RenderA2UiAsync(string.Empty);
        }
    }

    private async void CanvasSnapshotButton_Click(object sender, RoutedEventArgs e)
    {
        var app = (App)WpfApplication.Current;
        ShowCaptureIndicator("Canvas snapshot in progress");

        try
        {
            var result = await app.CanvasService.CaptureSnapshotAsync();
            if (!result.Success || result.Data == null)
            {
                CanvasSnapshotStatus.Text = result.Error ?? "Snapshot failed.";
                return;
            }

            AppPaths.EnsureDirectories();
            var dir = AppPaths.CanvasSnapshotsDir;
            Directory.CreateDirectory(dir);
            var timestamp = DateTimeOffset.Now.ToString("yyyyMMdd-HHmmss");
            var path = Path.Combine(dir, $"canvas-{timestamp}.png");
            await File.WriteAllBytesAsync(path, result.Data);
            _lastCanvasSnapshotPath = path;
            CanvasSnapshotStatus.Text = $"Snapshot saved: {path}";
        }
        finally
        {
            HideCaptureIndicator();
        }
    }

    private void CanvasOpenSnapshotButton_Click(object sender, RoutedEventArgs e)
    {
        var dir = AppPaths.CanvasSnapshotsDir;
        Directory.CreateDirectory(dir);
        Process.Start(new ProcessStartInfo
        {
            FileName = "explorer.exe",
            Arguments = dir,
            UseShellExecute = true
        });
    }

    private async void CanvasEvalButton_Click(object sender, RoutedEventArgs e)
    {
        var app = (App)WpfApplication.Current;
        CanvasEvalStatus.Text = "Running...";

        var script = CanvasEvalInput.Text;
        var result = await app.CanvasService.EvalAsync(script);
        CanvasEvalOutput.Text = result.Success ? (result.Result ?? "<no result>") : string.Empty;
        CanvasEvalStatus.Text = result.Success ? "OK" : result.Error ?? "Failed";
    }

    private void CanvasInstallButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = WebView2DownloadUrl,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            WpfMessageBox.Show($"Failed to open download page: {ex.Message}", "OpenClaw", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void PopulateScreens()
    {
        var app = (App)WpfApplication.Current;
        var screens = app.ScreenCaptureService.GetScreens();

        ScreenSelectComboBox.Items.Clear();
        ScreenSelectComboBox.Items.Add(new ScreenSelectionItem("All screens", true, null));

        for (var i = 0; i < screens.Count; i++)
        {
            var screen = screens[i];
            var label = $"{i + 1}. {screen.Name} ({screen.Bounds.Width}x{screen.Bounds.Height})" + (screen.Primary ? " [Primary]" : "");
            ScreenSelectComboBox.Items.Add(new ScreenSelectionItem(label, false, i));
        }

        ScreenSelectComboBox.SelectedIndex = 0;
    }

    private async void CaptureScreenButton_Click(object sender, RoutedEventArgs e)
    {
        var app = (App)WpfApplication.Current;
        var selection = ScreenSelectComboBox.SelectedItem as ScreenSelectionItem ?? new ScreenSelectionItem("All screens", true, null);

        ShowCaptureIndicator("Screen capture active");
        try
        {
            var result = await app.ScreenCaptureService.CaptureAsync(new ScreenCaptureRequest(selection.CaptureAllScreens, selection.ScreenIndex));
            if (!result.Success || result.PngData == null)
            {
                ScreenCaptureStatus.Text = result.Error ?? "Capture failed.";
                return;
            }

            AppPaths.EnsureDirectories();
            var dir = AppPaths.ScreenCapturesDir;
            Directory.CreateDirectory(dir);
            var timestamp = DateTimeOffset.Now.ToString("yyyyMMdd-HHmmss");
            var path = Path.Combine(dir, $"screen-{timestamp}.png");
            await File.WriteAllBytesAsync(path, result.PngData);
            _lastScreenCapturePath = path;
            ScreenCaptureStatus.Text = $"Capture saved: {path}";
        }
        finally
        {
            HideCaptureIndicator();
        }
    }

    private async void StartRecordingButton_Click(object sender, RoutedEventArgs e)
    {
        var app = (App)WpfApplication.Current;
        var selection = ScreenSelectComboBox.SelectedItem as ScreenSelectionItem ?? new ScreenSelectionItem("All screens", true, null);

        var options = new ScreenRecordingOptions(
            Interval: TimeSpan.FromSeconds(1),
            CaptureAllScreens: selection.CaptureAllScreens,
            ScreenIndex: selection.ScreenIndex);

        var session = await app.ScreenRecorder.StartAsync(options);
        ScreenCaptureStatus.Text = $"Recording started: {session.Directory}";
        ShowCaptureIndicator("Screen recording active");
        UpdateRecordingButtons();
    }

    private async void StopRecordingButton_Click(object sender, RoutedEventArgs e)
    {
        var app = (App)WpfApplication.Current;
        var session = await app.ScreenRecorder.StopAsync();
        HideCaptureIndicator();
        if (session == null)
        {
            ScreenCaptureStatus.Text = "No recording in progress.";
        }
        else
        {
            ScreenCaptureStatus.Text = $"Recording saved: {session.Directory} ({session.FramesCaptured} frames)";
        }

        UpdateRecordingButtons();
    }

    private void UpdateRecordingStatus(ScreenRecordingSession? session)
    {
        if (session == null)
        {
            UpdateRecordingButtons();
            return;
        }

        var message = session.LastError == null
            ? $"Recording... {session.FramesCaptured} frames"
            : $"Recording error: {session.LastError}";

        ScreenCaptureStatus.Text = message;
        UpdateRecordingButtons();
    }

    private void UpdateRecordingButtons()
    {
        var app = (App)WpfApplication.Current;
        var recording = app.ScreenRecorder.IsRecording;
        StartRecordingButton.IsEnabled = !recording;
        StopRecordingButton.IsEnabled = recording;
    }

    private void ShowCaptureIndicator(string text)
    {
        if (_captureIndicator == null)
        {
            _captureIndicator = new CaptureIndicatorWindow();
        }

        _captureIndicator.SetText(text);
        if (!_captureIndicator.IsVisible)
        {
            _captureIndicator.Show();
        }
    }

    private void HideCaptureIndicator()
    {
        if (_captureIndicator != null && _captureIndicator.IsVisible)
        {
            _captureIndicator.Hide();
        }
    }

    private sealed record ScreenSelectionItem(string Label, bool CaptureAllScreens, int? ScreenIndex)
    {
        public override string ToString() => Label;
    }
}
