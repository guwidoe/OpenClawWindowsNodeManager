using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using OpenClaw.Win.Core;
using WpfApplication = System.Windows.Application;
using WpfMessageBox = System.Windows.MessageBox;

namespace OpenClaw.Win.App;

public partial class MainWindow
{
    private readonly ObservableCollection<PendingApprovalItem> _pendingApprovals = new();
    private readonly ObservableCollection<ApprovalHistoryItem> _approvalHistory = new();

    private void InitializeApprovals()
    {
        PendingApprovalsList.ItemsSource = _pendingApprovals;
        ApprovalHistoryList.ItemsSource = _approvalHistory;
        PendingApprovalsList.SelectionChanged += (_, _) => UpdateApprovalButtons();

        ExecPolicyHelpText.Text = "Change via Settings or CLI: openclaw-win configure --exec-policy prompt|allow|deny";

        var app = (App)WpfApplication.Current;
        app.ExecApprovalService.ApprovalRequested += (_, _) => Dispatcher.Invoke(RefreshApprovals);
        app.ExecApprovalService.PendingChanged += (_, _) => Dispatcher.Invoke(RefreshApprovals);
        app.ExecApprovalService.HistoryChanged += (_, _) => Dispatcher.Invoke(RefreshApprovalHistory);

        RefreshApprovals();
        RefreshApprovalHistory();
    }

    private void RefreshApprovalsButton_Click(object sender, RoutedEventArgs e)
    {
        RefreshApprovals();
    }

    private void ApproveRequestButton_Click(object sender, RoutedEventArgs e)
    {
        HandleApprovalDecision(approved: true);
    }

    private void DenyRequestButton_Click(object sender, RoutedEventArgs e)
    {
        HandleApprovalDecision(approved: false);
    }

    private void RefreshApprovalHistoryButton_Click(object sender, RoutedEventArgs e)
    {
        RefreshApprovalHistory();
    }

    private void OpenApprovalLogButton_Click(object sender, RoutedEventArgs e)
    {
        if (!System.IO.File.Exists(AppPaths.ExecApprovalLogPath))
        {
            WpfMessageBox.Show("Approval history log not found yet.", "OpenClaw", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        Process.Start(new ProcessStartInfo
        {
            FileName = "notepad.exe",
            Arguments = AppPaths.ExecApprovalLogPath,
            UseShellExecute = true
        });
    }

    private void RefreshApprovals()
    {
        var app = (App)WpfApplication.Current;
        var pending = app.ExecApprovalService
            .GetPending()
            .OrderByDescending(item => item.RequestedAt)
            .ToList();

        _pendingApprovals.Clear();
        foreach (var item in pending)
        {
            _pendingApprovals.Add(new PendingApprovalItem(
                item.Id,
                item.Command,
                item.Arguments,
                item.RequestedBy ?? "Gateway",
                FormatTimestamp(item.RequestedAt)));
        }

        PendingApprovalsSummary.Text = pending.Count == 0
            ? "No pending approvals."
            : $"{pending.Count} pending approval(s).";

        UpdateApprovalButtons();
    }

    private void RefreshApprovalHistory()
    {
        var app = (App)WpfApplication.Current;
        var entries = app.ApprovalHistoryStore.ReadRecent(200)
            .OrderByDescending(item => item.DecidedAt)
            .ToList();

        _approvalHistory.Clear();
        foreach (var entry in entries)
        {
            _approvalHistory.Add(new ApprovalHistoryItem(
                entry.Command,
                entry.Arguments,
                entry.Decision.ToString(),
                FormatTimestamp(entry.DecidedAt)));
        }
    }

    private void UpdateApprovalButtons()
    {
        var hasSelection = PendingApprovalsList.SelectedItem != null;
        var isBusy = BusyPanel.Visibility == Visibility.Visible;
        ApproveRequestButton.IsEnabled = !isBusy && hasSelection;
        DenyRequestButton.IsEnabled = !isBusy && hasSelection;
    }

    private void HandleApprovalDecision(bool approved)
    {
        if (PendingApprovalsList.SelectedItem is not PendingApprovalItem item)
        {
            WpfMessageBox.Show("Select an approval request first.", "OpenClaw", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var app = (App)WpfApplication.Current;
        var ok = approved
            ? app.ExecApprovalService.TryApprove(item.Id)
            : app.ExecApprovalService.TryDeny(item.Id);

        if (!ok)
        {
            WpfMessageBox.Show("Approval request no longer pending.", "OpenClaw", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        RefreshApprovals();
        RefreshApprovalHistory();
    }

    private string FormatTimestamp(DateTimeOffset timestamp)
    {
        return timestamp.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss");
    }

    private void SetExecPolicySelection(ExecApprovalPolicy policy)
    {
        foreach (var item in ExecPolicyComboBox.Items.OfType<ComboBoxItem>())
        {
            if (item.Tag is string tag &&
                Enum.TryParse<ExecApprovalPolicy>(tag, true, out var parsed) &&
                parsed == policy)
            {
                ExecPolicyComboBox.SelectedItem = item;
                return;
            }
        }

        ExecPolicyComboBox.SelectedIndex = 0;
    }

    private ExecApprovalPolicy GetExecPolicySelection()
    {
        if (ExecPolicyComboBox.SelectedItem is ComboBoxItem item &&
            item.Tag is string tag &&
            Enum.TryParse<ExecApprovalPolicy>(tag, true, out var parsed))
        {
            return parsed;
        }

        return ExecApprovalPolicy.Prompt;
    }

    private sealed record PendingApprovalItem(
        string Id,
        string Command,
        string Arguments,
        string RequestedBy,
        string RequestedAt);

    private sealed record ApprovalHistoryItem(
        string Command,
        string Arguments,
        string Decision,
        string DecidedAt);
}
