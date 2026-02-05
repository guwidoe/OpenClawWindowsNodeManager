using System;
using System.Collections.Generic;
using System.Linq;

namespace OpenClaw.Win.Core;

public sealed class ExecApprovalService
{
    private readonly object _gate = new();
    private readonly List<ExecApprovalRequest> _pending = new();
    private readonly ExecApprovalHistoryStore _historyStore;
    private readonly Func<ExecApprovalPolicy> _policyProvider;

    public ExecApprovalService(ExecApprovalHistoryStore historyStore, Func<ExecApprovalPolicy>? policyProvider = null)
    {
        _historyStore = historyStore ?? throw new ArgumentNullException(nameof(historyStore));
        _policyProvider = policyProvider ?? (() => ExecApprovalPolicy.Prompt);
    }

    public event EventHandler<ExecApprovalRequest>? ApprovalRequested;
    public event EventHandler? PendingChanged;
    public event EventHandler? HistoryChanged;

    public IReadOnlyList<ExecApprovalRequest> GetPending()
    {
        lock (_gate)
        {
            return _pending.ToList();
        }
    }

    public ExecApprovalPolicy GetPolicy() => _policyProvider();

    public void Submit(ExecApprovalRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Id))
        {
            throw new ArgumentException("Request id is required.", nameof(request));
        }

        var policy = _policyProvider();
        if (policy == ExecApprovalPolicy.Allow)
        {
            RecordDecision(request, ExecApprovalDecision.Approved, policy);
            return;
        }

        if (policy == ExecApprovalPolicy.Deny)
        {
            RecordDecision(request, ExecApprovalDecision.Denied, policy);
            return;
        }

        lock (_gate)
        {
            if (_pending.Any(existing => existing.Id == request.Id))
            {
                return;
            }

            _pending.Add(request);
        }

        ApprovalRequested?.Invoke(this, request);
        PendingChanged?.Invoke(this, EventArgs.Empty);
    }

    public bool TryApprove(string requestId)
    {
        return Resolve(requestId, ExecApprovalDecision.Approved);
    }

    public bool TryDeny(string requestId)
    {
        return Resolve(requestId, ExecApprovalDecision.Denied);
    }

    private bool Resolve(string requestId, ExecApprovalDecision decision)
    {
        if (string.IsNullOrWhiteSpace(requestId))
        {
            return false;
        }

        ExecApprovalRequest? request = null;
        lock (_gate)
        {
            request = _pending.FirstOrDefault(item => item.Id == requestId);
            if (request == null)
            {
                return false;
            }

            _pending.Remove(request);
        }

        RecordDecision(request, decision, ExecApprovalPolicy.Prompt);
        PendingChanged?.Invoke(this, EventArgs.Empty);
        return true;
    }

    private void RecordDecision(ExecApprovalRequest request, ExecApprovalDecision decision, ExecApprovalPolicy policy)
    {
        var entry = ExecApprovalHistoryEntry.FromRequest(request, decision, policy);
        _historyStore.Append(entry);
        HistoryChanged?.Invoke(this, EventArgs.Empty);
    }
}
