using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
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
            var state = status.ToConnectionState();
            StatusLabel.Text = state.ToString().ToUpperInvariant();
            StatusLabel.Foreground = state switch
            {
                NodeConnectionState.Connected => Brushes.Green,
                NodeConnectionState.Degraded => Brushes.DarkGoldenrod,
                NodeConnectionState.Error => Brushes.Firebrick,
                _ => Brushes.DimGray
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
        });
    }

    private void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        LoadConfig();
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
    }

    private void SaveConfigButton_Click(object sender, RoutedEventArgs e)
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

        app.ConfigStore.Save(config);
        AutoStartManager.ApplyAutoStart(config.AutoStartTray);

        if (!string.IsNullOrWhiteSpace(GatewayTokenBox.Password))
        {
            app.TokenStore.SaveToken(GatewayTokenBox.Password);
            GatewayTokenBox.Clear();
        }

        WpfMessageBox.Show("Settings saved.", "OpenClaw", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private async void ConnectButton_Click(object sender, RoutedEventArgs e)
    {
        var app = (App)WpfApplication.Current;
        var status = await app.NodeService.ConnectAsync();
        UpdateStatus(status);
    }

    private async void DisconnectButton_Click(object sender, RoutedEventArgs e)
    {
        var app = (App)WpfApplication.Current;
        var status = await app.NodeService.DisconnectAsync();
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
}
