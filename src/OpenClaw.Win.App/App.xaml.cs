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
    private NodeStatus? _lastStatus;

    public ConfigStore ConfigStore { get; private set; } = null!;
    public TokenStore TokenStore { get; private set; } = null!;
    public OpenClawCliLocator CliLocator { get; private set; } = null!;
    public NodeService NodeService { get; private set; } = null!;
    public GatewayTester GatewayTester { get; private set; } = null!;
    public ChromeRelayService ChromeRelayService { get; private set; } = null!;

    private void OnStartup(object sender, StartupEventArgs e)
    {
        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        ConfigStore = new ConfigStore();
        TokenStore = new TokenStore();
        CliLocator = new OpenClawCliLocator();
        NodeService = new NodeService(ConfigStore, TokenStore, CliLocator);
        GatewayTester = new GatewayTester();
        ChromeRelayService = new ChromeRelayService();

        SeedConfigIfMissing();

        _mainWindow = new MainWindow();
        _mainWindow.Hide();

        var config = ConfigStore.Load();
        AutoStartManager.ApplyAutoStart(config.AutoStartTray);

        _trayIcon = new TrayIconService(
            onConnect: async () => await ConnectFromTrayAsync(),
            onDisconnect: async () => await DisconnectFromTrayAsync(),
            onToggle: async () => await ToggleFromTrayAsync(),
            onOpenSettings: ShowSettings,
            onOpenLogs: OpenLogsFolder,
            onOpenControlUi: OpenControlUi,
            onQuit: Shutdown);

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
            _lastStatus = status;

            _trayIcon?.UpdateStatus(status);
            _mainWindow?.UpdateStatus(status);
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

    private async Task ConnectFromTrayAsync()
    {
        var status = await NodeService.ConnectAsync();
        _trayIcon?.UpdateStatus(status);
        _mainWindow?.UpdateStatus(status);
    }

    private async Task DisconnectFromTrayAsync()
    {
        var status = await NodeService.DisconnectAsync();
        _trayIcon?.UpdateStatus(status);
        _mainWindow?.UpdateStatus(status);
    }

    private async Task ToggleFromTrayAsync()
    {
        var status = _lastStatus ?? await NodeService.GetStatusAsync();
        if (status.IsRunning)
        {
            await DisconnectFromTrayAsync();
        }
        else
        {
            await ConnectFromTrayAsync();
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
}
