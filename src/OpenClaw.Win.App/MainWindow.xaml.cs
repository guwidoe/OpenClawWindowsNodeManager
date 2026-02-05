using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using Brushes = System.Windows.Media.Brushes;
using WpfApplication = System.Windows.Application;
using WpfMessageBox = System.Windows.MessageBox;
using OpenClaw.Win.Core;

namespace OpenClaw.Win.App;

public partial class MainWindow : Window
{
    private const string DefaultSshCommand = "/home/qido/.local/share/fnm/fnm exec --using 22 -- openclaw dashboard --no-open";
    private NodeStatus? _lastStatus;

    public MainWindow()
    {
        InitializeComponent();
        Loaded += MainWindow_Loaded;
        Closing += MainWindow_Closing;
    }

    public void UpdateStatus(NodeStatus status)
    {
        Dispatcher.Invoke(() =>
        {
            _lastStatus = status;
            var state = status.ToConnectionState();
            StatusLabel.Text = state.ToString().ToUpperInvariant();
            StatusLabel.Foreground = state switch
            {
                NodeConnectionState.Connected => GetBrush("StatusConnectedBrush", Brushes.Green),
                NodeConnectionState.Degraded => GetBrush("StatusDegradedBrush", Brushes.DarkGoldenrod),
                NodeConnectionState.Error => GetBrush("StatusErrorBrush", Brushes.Firebrick),
                _ => GetBrush("StatusIdleBrush", Brushes.DimGray)
            };

            var details = status.GatewayHost == null
                ? "Gateway not configured."
                : $"Gateway: {status.GatewayHost}:{status.GatewayPort}";

            if (status.LastConnectedAt.HasValue)
            {
                details += $" | Last connected: {status.LastConnectedAt:yyyy-MM-dd HH:mm:ss}";
            }

            StatusDetails.Text = details;
            StatusError.Text = status.LastError ?? string.Empty;
            UpdateTokenBanner(status);
            UpdateTokenStatus();
        });
    }

    private void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        LoadConfig();
        UpdateTokenStatus();
        RefreshNodeHostLog();
        _ = ((App)WpfApplication.Current).RefreshStatusAsync();
    }

    private void MainWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        e.Cancel = true;
        Hide();
    }

    private void LoadConfig()
    {
        var app = (App)WpfApplication.Current;
        var config = app.ConfigStore.Load();

        GatewayHostTextBox.Text = config.GatewayHost;
        GatewayPortTextBox.Text = config.GatewayPort.ToString();
        UseTlsCheckBox.IsChecked = config.UseTls;
        TlsFingerprintTextBox.Text = config.TlsFingerprint ?? string.Empty;
        DisplayNameTextBox.Text = config.DisplayName;
        ControlUiTextBox.Text = config.ControlUiUrl;
        RelayPortTextBox.Text = config.RelayPort.ToString();
        AutoStartCheckBox.IsChecked = config.AutoStartTray;
        CaptureNodeOutputCheckBox.IsChecked = config.CaptureNodeHostOutput;
        ThemeCheckBox.IsChecked = config.UseDarkTheme;
        TrayNotificationsCheckBox.IsChecked = config.EnableTrayNotifications;
        SshHostTextBox.Text = config.SshHost ?? string.Empty;
        SshUserTextBox.Text = config.SshUser ?? string.Empty;
        SshPortTextBox.Text = (config.SshPort == 0 ? 22 : config.SshPort).ToString();
        SshCommandTextBox.Text = string.IsNullOrWhiteSpace(config.SshCommand) ? DefaultSshCommand : config.SshCommand;
    }

    private async void SaveConfigButton_Click(object sender, RoutedEventArgs e)
    {
        var app = (App)WpfApplication.Current;
        var config = app.ConfigStore.Load();

        config.GatewayHost = GatewayHostTextBox.Text.Trim();
        if (int.TryParse(GatewayPortTextBox.Text.Trim(), out var port))
        {
            config.GatewayPort = port;
        }

        config.UseTls = UseTlsCheckBox.IsChecked == true;
        config.TlsFingerprint = string.IsNullOrWhiteSpace(TlsFingerprintTextBox.Text)
            ? null
            : TlsFingerprintTextBox.Text.Trim();
        config.DisplayName = DisplayNameTextBox.Text.Trim();
        config.ControlUiUrl = ControlUiTextBox.Text.Trim();
        if (int.TryParse(RelayPortTextBox.Text.Trim(), out var relayPort))
        {
            config.RelayPort = relayPort;
        }

        config.AutoStartTray = AutoStartCheckBox.IsChecked == true;
        config.CaptureNodeHostOutput = CaptureNodeOutputCheckBox.IsChecked == true;
        config.UseDarkTheme = ThemeCheckBox.IsChecked == true;
        config.EnableTrayNotifications = TrayNotificationsCheckBox.IsChecked == true;
        config.SshHost = string.IsNullOrWhiteSpace(SshHostTextBox.Text) ? null : SshHostTextBox.Text.Trim();
        config.SshUser = string.IsNullOrWhiteSpace(SshUserTextBox.Text) ? null : SshUserTextBox.Text.Trim();
        if (int.TryParse(SshPortTextBox.Text.Trim(), out var sshPort))
        {
            config.SshPort = sshPort;
        }

        config.SshCommand = string.IsNullOrWhiteSpace(SshCommandTextBox.Text) ? DefaultSshCommand : SshCommandTextBox.Text.Trim();

        app.ConfigStore.Save(config);
        AutoStartManager.ApplyAutoStart(config.AutoStartTray);
        ThemeManager.ApplyTheme(config.UseDarkTheme);
        app.UpdateTrayNotifications(config.EnableTrayNotifications);

        if (!string.IsNullOrWhiteSpace(GatewayTokenBox.Password))
        {
            app.TokenStore.SaveToken(GatewayTokenBox.Password);
            GatewayTokenBox.Clear();
        }

        UpdateTokenStatus();

        if (config.CaptureNodeHostOutput)
        {
            await app.NodeService.EnsureNodeHostOutputCaptureAsync();
        }

        WpfMessageBox.Show("Settings saved.", "OpenClaw", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private async void ConnectButton_Click(object sender, RoutedEventArgs e)
    {
        var app = (App)WpfApplication.Current;
        var status = await app.ConnectAsync();
        UpdateStatus(status);
    }

    private async void DisconnectButton_Click(object sender, RoutedEventArgs e)
    {
        var app = (App)WpfApplication.Current;
        var status = await app.DisconnectAsync();
        UpdateStatus(status);
    }

    private async void RefreshButton_Click(object sender, RoutedEventArgs e)
    {
        await ((App)WpfApplication.Current).RefreshStatusAsync();
    }

    private async void TestGatewayButton_Click(object sender, RoutedEventArgs e)
    {
        var app = (App)WpfApplication.Current;
        var config = app.ConfigStore.Load();
        var result = await app.GatewayTester.TestAsync(config);

        var message = $"DNS: {(result.DnsResolved ? "ok" : "fail")}\n" +
                      $"TCP: {(result.TcpConnected ? "ok" : "fail")}\n" +
                      $"TLS: {(result.TlsHandshake ? "ok" : "fail")}\n" +
                      $"HTTP: {(result.HttpReachable ? "ok" : "fail")}\n" +
                      $"WebSocket: {(result.WebSocketConnected ? "ok" : "fail")}";

        if (!string.IsNullOrWhiteSpace(result.ErrorMessage))
        {
            message += $"\nError: {result.ErrorMessage}";
        }

        WpfMessageBox.Show(message, "Gateway Test", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private async void VerifyRelayButton_Click(object sender, RoutedEventArgs e)
    {
        var app = (App)WpfApplication.Current;
        var config = app.ConfigStore.Load();
        var ok = await app.ChromeRelayService.VerifyRelayAsync(config.RelayPort);
        WpfMessageBox.Show(ok ? "Relay reachable." : "Relay not reachable.", "Chrome Relay", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void OpenExtensionsButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "chrome://extensions",
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            Log.Error("Failed to open chrome extensions.", ex);
        }
    }

    private void OpenRelayFolderButton_Click(object sender, RoutedEventArgs e)
    {
        var defaultPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".openclaw", "extensions");
        if (!Directory.Exists(defaultPath))
        {
            WpfMessageBox.Show("Extension folder not found. Locate your OpenClaw extension folder manually.", "OpenClaw", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        Process.Start(new ProcessStartInfo
        {
            FileName = "explorer.exe",
            Arguments = defaultPath,
            UseShellExecute = true
        });
    }

    private void OpenLogsButton_Click(object sender, RoutedEventArgs e)
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = "explorer.exe",
            Arguments = AppPaths.LogsDir,
            UseShellExecute = true
        });
    }

    private void OpenNodeHostLogButton_Click(object sender, RoutedEventArgs e)
    {
        if (!File.Exists(AppPaths.NodeHostLogPath))
        {
            WpfMessageBox.Show("Node host log not found yet.", "OpenClaw", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        Process.Start(new ProcessStartInfo
        {
            FileName = "notepad.exe",
            Arguments = AppPaths.NodeHostLogPath,
            UseShellExecute = true
        });
    }

    private void RefreshNodeHostLogButton_Click(object sender, RoutedEventArgs e)
    {
        RefreshNodeHostLog();
    }

    private void OpenTokenSettingsButton_Click(object sender, RoutedEventArgs e)
    {
        MainTabs.SelectedIndex = 0;
        GatewayTokenBox.Focus();
    }

    private void OpenControlUiButton_Click(object sender, RoutedEventArgs e)
    {
        var app = (App)WpfApplication.Current;
        var config = app.ConfigStore.Load();
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

    private void UpdateTokenBanner(NodeStatus status)
    {
        TokenBanner.Visibility = status.Issue == NodeIssue.TokenMissing
            ? Visibility.Visible
            : Visibility.Collapsed;

        PairingBanner.Visibility = status.Issue == NodeIssue.PairingRequired
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    public void SetBusy(bool isBusy, string? title, string? detail)
    {
        Dispatcher.Invoke(() =>
        {
            BusyPanel.Visibility = isBusy ? Visibility.Visible : Visibility.Collapsed;
            BusyText.Text = detail ?? string.Empty;

            ConnectButton.IsEnabled = !isBusy;
            DisconnectButton.IsEnabled = !isBusy;
            RefreshButton.IsEnabled = !isBusy;
            InstallButton.IsEnabled = !isBusy;
            UninstallButton.IsEnabled = !isBusy;
            StartButton.IsEnabled = !isBusy;
            StopButton.IsEnabled = !isBusy;
            FetchTokenButton.IsEnabled = !isBusy;
            OpenNodeHostLogButton.IsEnabled = !isBusy;
            RefreshNodeHostLogButton.IsEnabled = !isBusy;

            if (isBusy && !string.IsNullOrWhiteSpace(title))
            {
                StatusLabel.Text = title.ToUpperInvariant();
                StatusLabel.Foreground = GetBrush("StatusBusyBrush", Brushes.DarkSlateBlue);
                StatusError.Text = string.Empty;
            }
            else if (_lastStatus != null)
            {
                UpdateStatus(_lastStatus);
            }
        });
    }

    private System.Windows.Media.Brush GetBrush(string key, System.Windows.Media.Brush fallback)
    {
        if (TryFindResource(key) is System.Windows.Media.Brush brush)
        {
            return brush;
        }

        return fallback;
    }

    private void UpdateTokenStatus()
    {
        var app = (App)WpfApplication.Current;
        TokenSavedLabel.Text = app.TokenStore.HasToken
            ? "Token stored securely (DPAPI)."
            : "No token stored yet.";
    }

    private async void InstallButton_Click(object sender, RoutedEventArgs e)
    {
        var app = (App)WpfApplication.Current;
        try
        {
            await app.NodeService.InstallAsync();
            WpfMessageBox.Show("Install command issued.", "OpenClaw", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            WpfMessageBox.Show($"Install failed: {ex.Message}", "OpenClaw", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async void UninstallButton_Click(object sender, RoutedEventArgs e)
    {
        var app = (App)WpfApplication.Current;
        try
        {
            await app.NodeService.UninstallAsync();
            WpfMessageBox.Show("Uninstall command issued.", "OpenClaw", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            WpfMessageBox.Show($"Uninstall failed: {ex.Message}", "OpenClaw", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async void ExportDiagnosticsButton_Click(object sender, RoutedEventArgs e)
    {
        var app = (App)WpfApplication.Current;
        var config = app.ConfigStore.Load();
        var status = await app.NodeService.GetStatusAsync();

        AppPaths.EnsureDirectories();
        var timestamp = DateTimeOffset.Now.ToString("yyyyMMdd-HHmmss");
        var zipPath = Path.Combine(AppPaths.DiagnosticsDir, $"diagnostics-{timestamp}.zip");

        using (var archive = ZipFile.Open(zipPath, ZipArchiveMode.Create))
        {
            var redactedConfig = new
            {
                config.GatewayHost,
                config.GatewayPort,
                config.UseTls,
                config.TlsFingerprint,
                config.DisplayName,
                config.ControlUiUrl,
                config.RelayPort,
                config.AutoStartTray,
                config.CaptureNodeHostOutput,
                config.UseDarkTheme,
                config.EnableTrayNotifications,
                GatewayToken = "<redacted>"
            };

            var configEntry = archive.CreateEntry("config.json");
            await using (var writer = new StreamWriter(configEntry.Open()))
            {
                await writer.WriteAsync(JsonSerializer.Serialize(redactedConfig, new JsonSerializerOptions { WriteIndented = true }));
            }

            var statusEntry = archive.CreateEntry("status.json");
            await using (var writer = new StreamWriter(statusEntry.Open()))
            {
                await writer.WriteAsync(JsonSerializer.Serialize(status, new JsonSerializerOptions { WriteIndented = true }));
            }

            AddLogToArchive(archive, AppPaths.AppLogPath, "app.log");
            AddLogToArchive(archive, AppPaths.NodeLogPath, "node.log");
            AddLogToArchive(archive, AppPaths.NodeHostLogPath, "node-host.log");
        }

        DiagnosticsStatus.Text = $"Diagnostics exported to {zipPath}";
    }

    private static void AddLogToArchive(ZipArchive archive, string path, string entryName)
    {
        if (!File.Exists(path))
        {
            return;
        }

        var entry = archive.CreateEntry(entryName);
        using var entryStream = entry.Open();
        using var fileStream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        fileStream.CopyTo(entryStream);
    }

    private void RefreshNodeHostLog()
    {
        try
        {
            if (!File.Exists(AppPaths.NodeHostLogPath))
            {
                NodeHostLogTextBox.Text = "No node host output captured yet.";
                return;
            }

            var lines = File.ReadAllLines(AppPaths.NodeHostLogPath);
            var tail = lines.Skip(Math.Max(0, lines.Length - 200));
            NodeHostLogTextBox.Text = string.Join(Environment.NewLine, tail);
            NodeHostLogTextBox.ScrollToEnd();
        }
        catch (Exception ex)
        {
            NodeHostLogTextBox.Text = $"Failed to read node host log: {ex.Message}";
        }
    }

    private async void FetchTokenButton_Click(object sender, RoutedEventArgs e)
    {
        var app = (App)WpfApplication.Current;
        var config = app.ConfigStore.Load();

        var host = string.IsNullOrWhiteSpace(SshHostTextBox.Text) ? config.SshHost : SshHostTextBox.Text.Trim();
        var user = string.IsNullOrWhiteSpace(SshUserTextBox.Text) ? config.SshUser : SshUserTextBox.Text.Trim();
        var command = string.IsNullOrWhiteSpace(SshCommandTextBox.Text) ? (config.SshCommand ?? DefaultSshCommand) : SshCommandTextBox.Text.Trim();
        var port = config.SshPort;
        if (int.TryParse(SshPortTextBox.Text.Trim(), out var parsedPort))
        {
            port = parsedPort;
        }

        SshFetchStatus.Text = "Connecting...";

        var fetcher = new SshTokenFetcher();
        var result = await fetcher.FetchAsync(host ?? string.Empty, user ?? string.Empty, port, command);

        if (result.Success && !string.IsNullOrWhiteSpace(result.Token))
        {
            app.TokenStore.SaveToken(result.Token);
            UpdateTokenStatus();
            SshFetchStatus.Text = "Token saved.";
            WpfMessageBox.Show("Gateway token fetched and saved.", "OpenClaw", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        else
        {
            var message = result.ErrorMessage ?? "Token fetch failed.";
            SshFetchStatus.Text = message;
            if (!string.IsNullOrWhiteSpace(result.Output))
            {
                message += "\n\nDetails:\n" + result.Output;
            }

            WpfMessageBox.Show(message, "OpenClaw", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }
}
