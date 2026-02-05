using System;
using System.Threading.Tasks;
using OpenClaw.Win.Core;
using Xunit;

namespace OpenClaw.Win.Core.Tests;

[Collection("StateDir")]
public class ExecApprovalServiceTests
{
    [Fact]
    public async Task Submit_RaisesApprovalRequestedWithinThreeSeconds()
    {
        using var temp = new TempStateDir();
        var history = new ExecApprovalHistoryStore();
        var service = new ExecApprovalService(history, () => ExecApprovalPolicy.Prompt);

        var tcs = new TaskCompletionSource<ExecApprovalRequest>(TaskCreationOptions.RunContinuationsAsynchronously);
        service.ApprovalRequested += (_, request) => tcs.TrySetResult(request);

        var request = ExecApprovalRequest.Create("openclaw", "node status --json", "gateway");
        service.Submit(request);

        var completed = await Task.WhenAny(tcs.Task, Task.Delay(TimeSpan.FromSeconds(3)));
        Assert.Same(tcs.Task, completed);
    }

    [Fact]
    public void Approve_RemovesPendingAndLogsHistory()
    {
        using var temp = new TempStateDir();
        var history = new ExecApprovalHistoryStore();
        var service = new ExecApprovalService(history, () => ExecApprovalPolicy.Prompt);

        var request = ExecApprovalRequest.Create("openclaw", "node restart --json", "gateway");
        service.Submit(request);

        var ok = service.TryApprove(request.Id);

        Assert.True(ok);
        Assert.Empty(service.GetPending());

        var entries = history.ReadRecent(10);
        Assert.Single(entries);
        Assert.Equal(ExecApprovalDecision.Approved, entries[0].Decision);
        Assert.Equal(request.Command, entries[0].Command);
    }

    [Fact]
    public void Deny_LogsHistoryWhenPolicyIsDeny()
    {
        using var temp = new TempStateDir();
        var history = new ExecApprovalHistoryStore();
        var service = new ExecApprovalService(history, () => ExecApprovalPolicy.Deny);

        var request = ExecApprovalRequest.Create("openclaw", "node stop --json", "gateway");
        service.Submit(request);

        Assert.Empty(service.GetPending());
        var entries = history.ReadRecent(5);
        Assert.Single(entries);
        Assert.Equal(ExecApprovalDecision.Denied, entries[0].Decision);
        Assert.Equal(ExecApprovalPolicy.Deny, entries[0].Policy);
    }
}
