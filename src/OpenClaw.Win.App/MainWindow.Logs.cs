using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using OpenClaw.Win.Core;
using WpfApplication = System.Windows.Application;
using WpfMessageBox = System.Windows.MessageBox;

namespace OpenClaw.Win.App;

public partial class MainWindow
{
    private void OpenLogsButton_Click(object sender, RoutedEventArgs e)
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = "explorer.exe",
            Arguments = AppPaths.LogsDir,
            UseShellExecute = true
        });
    }

    private void OpenAppLogButton_Click(object sender, RoutedEventArgs e)
    {
        if (!File.Exists(AppPaths.AppLogPath))
        {
            WpfMessageBox.Show("App log not found yet.", "OpenClaw", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        Process.Start(new ProcessStartInfo
        {
            FileName = "notepad.exe",
            Arguments = AppPaths.AppLogPath,
            UseShellExecute = true
        });
    }

    private void OpenNodeLogButton_Click(object sender, RoutedEventArgs e)
    {
        if (!File.Exists(AppPaths.NodeLogPath))
        {
            WpfMessageBox.Show("Node log not found yet.", "OpenClaw", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        Process.Start(new ProcessStartInfo
        {
            FileName = "notepad.exe",
            Arguments = AppPaths.NodeLogPath,
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
                config.ThemePreference,
                config.EnableTrayNotifications,
                config.EnableSystemNotifications,
                config.ExecApprovalPolicy,
                GatewayToken = "<redacted>"
            };

            var summaryEntry = archive.CreateEntry("summary.txt");
            await using (var writer = new StreamWriter(summaryEntry.Open()))
            {
                var version = typeof(App).Assembly.GetName().Version?.ToString() ?? "unknown";
                await writer.WriteLineAsync($"OpenClaw Windows Companion diagnostics");
                await writer.WriteLineAsync($"Version: {version}");
                await writer.WriteLineAsync($"Exported: {DateTimeOffset.Now:O}");
                await writer.WriteLineAsync();
                await writer.WriteLineAsync($"State: {status.ToConnectionState()}");
                await writer.WriteLineAsync($"Running: {status.IsRunning}");
                await writer.WriteLineAsync($"Connected: {status.IsConnected}");
                await writer.WriteLineAsync($"Gateway: {status.GatewayHost}:{status.GatewayPort}");
                if (!string.IsNullOrWhiteSpace(status.LastError))
                {
                    await writer.WriteLineAsync($"LastError: {status.LastError}");
                }

                await writer.WriteLineAsync();
                await writer.WriteLineAsync("Included files:");
                await writer.WriteLineAsync("- config.json");
                await writer.WriteLineAsync("- status.json");
                await writer.WriteLineAsync("- app.log");
                await writer.WriteLineAsync("- node.log");
                await writer.WriteLineAsync("- node-host.log");
            }

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

        _lastDiagnosticsPath = zipPath;
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

    private void CopyDiagnosticsPathButton_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_lastDiagnosticsPath))
        {
            WpfMessageBox.Show("Export diagnostics first to copy the path.", "OpenClaw", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        System.Windows.Clipboard.SetText(_lastDiagnosticsPath);
        WpfMessageBox.Show("Diagnostics path copied to clipboard.", "OpenClaw", MessageBoxButton.OK, MessageBoxImage.Information);
    }
}
