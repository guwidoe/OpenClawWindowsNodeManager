using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using OpenClaw.Win.Core;
using WpfMessageBox = System.Windows.MessageBox;

namespace OpenClaw.Win.App;

public partial class App : System.Windows.Application
{
    private TrayIconService? _trayIcon;
    private MainWindow? _mainWindow;
    private DispatcherTimer? _pollTimer;
    private readonly SemaphoreSlim _statusLock = new(1, 1);
    private readonly StatusStabilizer _statusStabilizer = new();
    private NodeStatus? _lastStatus;
    private bool _isBusy;
    private string? _busyTitle;
    private DateTimeOffset _busyStart;
    private int _busyStep;

    public ConfigStore ConfigStore { get; private set; } = null!;
    public TokenStore TokenStore { get; private set; } = null!;
    public OpenClawCliLocator CliLocator { get; private set; } = null!;
    public NodeService NodeService { get; private set; } = null!;
    public GatewayTester GatewayTester { get; private set; } = null!;
    public ChromeRelayService ChromeRelayService { get; private set; } = null!;
    public ExecApprovalHistoryStore ApprovalHistoryStore { get; private set; } = null!;
    public ExecApprovalService ExecApprovalService { get; private set; } = null!;
    public SystemNotificationBridge SystemNotifications { get; private set; } = null!;
    public CanvasService CanvasService { get; private set; } = null!;
    public IScreenCaptureService ScreenCaptureService { get; private set; } = null!;
    public ScreenRecorder ScreenRecorder { get; private set; } = null!;
    public BrowserRelayStatusService BrowserRelayStatusService { get; private set; } = null!;
    private NodeStatus? _lastNotifiedStatus;

    private void OnStartup(object sender, StartupEventArgs e)
    {
        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        ConfigStore = new ConfigStore();
        TokenStore = new TokenStore();
        CliLocator = new OpenClawCliLocator();
        NodeService = new NodeService(ConfigStore, TokenStore, CliLocator);
        GatewayTester = new GatewayTester();
        ChromeRelayService = new ChromeRelayService();
        ApprovalHistoryStore = new ExecApprovalHistoryStore();
        ExecApprovalService = new ExecApprovalService(ApprovalHistoryStore, () => ConfigStore.Load().ExecApprovalPolicy);
        CanvasService = new CanvasService();
        ScreenCaptureService = new ScreenCaptureService(new DefaultScreenInfoProvider(), new DefaultScreenImageProvider());
        ScreenRecorder = new ScreenRecorder(ScreenCaptureService);
        BrowserRelayStatusService = new BrowserRelayStatusService(ChromeRelayService);

        SeedConfigIfMissing();

        var config = ConfigStore.Load();
        ThemeManager.ApplyTheme(config.ThemePreference);

        _mainWindow = new MainWindow();
        _mainWindow.Hide();

        AutoStartManager.ApplyAutoStart(config.AutoStartTray);

        _trayIcon = new TrayIconService(
            onConnect: async () => await ConnectFromTrayAsync(),
            onDisconnect: async () => await DisconnectFromTrayAsync(),
            onToggle: async () => await ToggleFromTrayAsync(),
            onOpenSettings: ShowSettings,
            onOpenLogs: OpenLogsFolder,
            onOpenControlUi: OpenControlUi,
            onQuit: Shutdown);
        _trayIcon.SetNotificationsEnabled(config.EnableTrayNotifications);

        var rateLimiter = new NotificationRateLimiter(TimeSpan.FromSeconds(30));
        var router = new SystemNotificationRouter(rateLimiter);
        var publisher = new TrayIconNotificationPublisher(_trayIcon);
        SystemNotifications = new SystemNotificationBridge(router, publisher);
        SystemNotifications.SetEnabled(config.EnableSystemNotifications);

        ExecApprovalService.ApprovalRequested += (_, request) => NotifyApprovalRequested(request);
        ExecApprovalService.HistoryChanged += (_, _) => NotifyApprovalHistoryChange();

        StartPolling(config.PollIntervalSeconds);
    }

    private void OnExit(object sender, ExitEventArgs e)
    {
        _pollTimer?.Stop();
        _trayIcon?.Dispose();
    }

    public async Task RefreshStatusAsync()
    {
        if (!await _statusLock.WaitAsync(0))
        {
            return;
        }

        try
        {
            var status = await NodeService.GetStatusAsync();

            if (status.IsStatusCheckFailed && _lastStatus != null)
            {
                Log.Warn($"Status check failed: {status.LastError ?? "unknown"}");
                return;
            }

            _lastStatus = status;

            if (_isBusy)
            {
                return;
            }

            var stable = _statusStabilizer.Stabilize(status);
            _trayIcon?.UpdateStatus(stable);
            _mainWindow?.UpdateStatus(stable);
            NotifyStatusChange(stable);
        }
        catch (Exception ex)
        {
            Log.Error("Status refresh failed.", ex);
        }
        finally
        {
            _statusLock.Release();
        }
    }

    private void StartPolling(int intervalSeconds)
    {
        _pollTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(Math.Max(2, intervalSeconds))
        };

        _pollTimer.Tick += async (_, _) => await RefreshStatusAsync();
        _pollTimer.Start();

        _ = RefreshStatusAsync();
    }

    public Task<NodeStatus> ConnectAsync()
    {
        return RunWithBusyAsync("Connecting...", "Connect", progress => NodeService.ConnectAsync(progress: progress));
    }

    public Task<NodeStatus> DisconnectAsync()
    {
        return RunWithBusyAsync("Disconnecting...", "Disconnect", progress => NodeService.DisconnectAsync(progress: progress));
    }

    private Task ConnectFromTrayAsync() => ConnectAsync();

    private Task DisconnectFromTrayAsync() => DisconnectAsync();

    private async Task ToggleFromTrayAsync()
    {
        var status = _lastStatus ?? await NodeService.GetStatusAsync();
        if (status.IsRunning)
        {
            await DisconnectAsync();
        }
        else
        {
            await ConnectAsync();
        }
    }

    private void ShowSettings()
    {
        if (_mainWindow == null)
        {
            return;
        }

        if (!_mainWindow.IsVisible)
        {
            _mainWindow.Show();
        }

        _mainWindow.WindowState = WindowState.Normal;
        _mainWindow.Activate();
    }

    private void OpenLogsFolder()
    {
        try
        {
            AppPaths.EnsureDirectories();
            Process.Start(new ProcessStartInfo
            {
                FileName = "explorer.exe",
                Arguments = AppPaths.LogsDir,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            Log.Error("Failed to open logs folder.", ex);
        }
    }

    private void OpenControlUi()
    {
        try
        {
            var config = ConfigStore.Load();
            if (string.IsNullOrWhiteSpace(config.ControlUiUrl))
            {
                WpfMessageBox.Show("Control UI URL is not set in Settings.", "OpenClaw", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            Process.Start(new ProcessStartInfo
            {
                FileName = config.ControlUiUrl,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            Log.Error("Failed to open control UI.", ex);
        }
    }

    private async Task<NodeStatus> RunWithBusyAsync(string message, string actionLabel, Func<IProgress<string>, Task<NodeStatus>> action)
    {
        if (_isBusy)
        {
            return _lastStatus ?? new NodeStatus();
        }

        _isBusy = true;
        _busyTitle = message;
        _busyStart = DateTimeOffset.UtcNow;
        _busyStep = 1;
        UpdateBusyMessage(message, FormatBusyDetail(message, _busyStep));
        var progress = new Progress<string>(progressMessage =>
        {
            if (string.IsNullOrWhiteSpace(progressMessage))
            {
                return;
            }

            _busyStep++;
            UpdateBusyMessage(message, FormatBusyDetail(progressMessage, _busyStep));
        });

        NodeStatus status;
        try
        {
            status = await action(progress).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Log.Error("Operation failed.", ex);
            status = _lastStatus ?? new NodeStatus { Issue = NodeIssue.UnknownError, LastError = ex.Message };
        }
        finally
        {
            _isBusy = false;
            _mainWindow?.SetBusy(false, null, null);
        }

        if (status.IsStatusCheckFailed && _lastStatus != null)
        {
            return _lastStatus;
        }

        _statusStabilizer.Reset();
        _lastStatus = status;
        _trayIcon?.UpdateStatus(status);
        _mainWindow?.UpdateStatus(status);
        NotifyStatusChange(status);
        UpdateLastAction(actionLabel, status);
        return status;
    }

    private void UpdateBusyMessage(string title, string detail)
    {
        if (!_isBusy)
        {
            return;
        }

        _trayIcon?.SetBusy(detail);
        _mainWindow?.SetBusy(true, title, detail);
    }

    public void UpdateTrayNotifications(bool enabled)
    {
        _trayIcon?.SetNotificationsEnabled(enabled);
    }

    public void UpdateSystemNotifications(bool enabled)
    {
        SystemNotifications?.SetEnabled(enabled);
    }

    private void UpdateLastAction(string actionLabel, NodeStatus status)
    {
        var timestamp = DateTimeOffset.Now.ToString("yyyy-MM-dd HH:mm:ss");
        var success = actionLabel.Equals("Connect", StringComparison.OrdinalIgnoreCase)
            ? status.IsRunning && status.IsConnected
            : !status.IsRunning;

        var result = success ? "succeeded" : "failed";
        var message = $"Last action: {actionLabel} {result} at {timestamp}";
        if (!success && !string.IsNullOrWhiteSpace(status.LastError))
        {
            message += $" ({status.LastError})";
        }

        _mainWindow?.SetLastAction(message);
    }

    private string FormatBusyDetail(string message, int step)
    {
        var elapsed = DateTimeOffset.UtcNow - _busyStart;
        var elapsedText = elapsed.ToString(@"mm\:ss");
        return $"{step}. {message} ({elapsedText})";
    }

    private void SeedConfigIfMissing()
    {
        try
        {
            if (File.Exists(AppPaths.ConfigPath))
            {
                return;
            }

            var seedPath = Path.Combine(AppContext.BaseDirectory, "personal-config.json");
            if (!File.Exists(seedPath))
            {
                return;
            }

            AppPaths.EnsureDirectories();
            File.Copy(seedPath, AppPaths.ConfigPath, overwrite: false);
            Log.Info($"Seeded config from {seedPath}.");
        }
        catch (Exception ex)
        {
            Log.Error("Failed to seed config.", ex);
        }
    }

    private void NotifyStatusChange(NodeStatus current)
    {
        if (SystemNotifications == null)
        {
            return;
        }

        var previous = _lastNotifiedStatus;
        _lastNotifiedStatus = current;

        if (previous == null)
        {
            return;
        }

        if (previous.ToConnectionState() == current.ToConnectionState() && previous.Issue == current.Issue)
        {
            return;
        }

        var state = current.ToConnectionState();
        var title = $"OpenClaw {state}";
        var message = string.IsNullOrWhiteSpace(current.LastError)
            ? $"Status: {state}"
            : $"{state}: {current.LastError}";

        var kind = state == NodeConnectionState.Error
            ? SystemNotificationKind.Error
            : SystemNotificationKind.StatusChange;

        SystemNotifications.Notify(new SystemNotificationEvent(kind, title, message));
    }

    private void NotifyApprovalRequested(ExecApprovalRequest request)
    {
        SystemNotifications?.Notify(new SystemNotificationEvent(
            SystemNotificationKind.ApprovalRequested,
            "Approval required",
            $"{request.Command} {request.Arguments}".Trim() + "\nOpen Settings → Approvals to respond."));
    }

    private void NotifyApprovalHistoryChange()
    {
        // History updates are surfaced in UI; no toast by default to avoid spam.
    }
}
