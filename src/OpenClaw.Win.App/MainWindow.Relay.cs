using System;
using System.Threading.Tasks;
using System.Windows;
using OpenClaw.Win.Core;
using WpfApplication = System.Windows.Application;

namespace OpenClaw.Win.App;

public partial class MainWindow
{
    private BrowserRelayStatus? _lastRelayStatus;

    private async Task InitializeRelayStatusAsync()
    {
        await UpdateRelayStatusAsync();
    }

    private async Task UpdateRelayStatusAsync()
    {
        var app = (App)WpfApplication.Current;
        var config = app.ConfigStore.Load();
        var status = await app.BrowserRelayStatusService.CheckAsync(config.RelayPort);
        _lastRelayStatus = status;
        UpdateRelayStatusUI(status);
    }

    private void UpdateRelayStatusUI(BrowserRelayStatus status)
    {
        RelayStatusTitle.Text = status.Title;
        RelayStatusDetails.Text = status.Message;
        RelayStatusDetails.Foreground = status.State switch
        {
            BrowserRelayState.Connected => GetBrush("StatusConnectedBrush", System.Windows.Media.Brushes.Green),
            BrowserRelayState.Disconnected => GetBrush("StatusDegradedBrush", System.Windows.Media.Brushes.DarkGoldenrod),
            BrowserRelayState.Error => GetBrush("StatusErrorBrush", System.Windows.Media.Brushes.Firebrick),
            _ => GetBrush("TextSecondaryBrush", System.Windows.Media.Brushes.DimGray)
        };
    }

    private async void VerifyRelayButton_Click(object sender, RoutedEventArgs e)
    {
        var app = (App)WpfApplication.Current;
        var config = app.ConfigStore.Load();

        RelayStatusTitle.Text = "Checking relay...";
        RelayStatusDetails.Text = "Contacting the local relay endpoint.";

        var status = await app.BrowserRelayStatusService.CheckAsync(config.RelayPort);
        _lastRelayStatus = status;
        UpdateRelayStatusUI(status);
    }
}
