using System;

namespace OpenClaw.Win.Core;

public enum ExecApprovalDecision
{
    Approved,
    Denied
}

public enum ExecApprovalPolicy
{
    Prompt,
    Allow,
    Deny
}

public sealed record ExecApprovalRequest(
    string Id,
    string Command,
    string Arguments,
    string? RequestedBy,
    DateTimeOffset RequestedAt,
    string? Reason)
{
    public static ExecApprovalRequest Create(
        string command,
        string arguments,
        string? requestedBy = null,
        string? reason = null,
        DateTimeOffset? requestedAt = null,
        string? id = null)
    {
        return new ExecApprovalRequest(
            id ?? Guid.NewGuid().ToString("N"),
            command,
            arguments,
            requestedBy,
            requestedAt ?? DateTimeOffset.UtcNow,
            reason);
    }
}

public sealed record ExecApprovalHistoryEntry(
    string Id,
    string Command,
    string Arguments,
    string? RequestedBy,
    DateTimeOffset RequestedAt,
    DateTimeOffset DecidedAt,
    ExecApprovalDecision Decision,
    ExecApprovalPolicy Policy,
    string? Reason)
{
    public static ExecApprovalHistoryEntry FromRequest(
        ExecApprovalRequest request,
        ExecApprovalDecision decision,
        ExecApprovalPolicy policy,
        DateTimeOffset? decidedAt = null)
    {
        return new ExecApprovalHistoryEntry(
            request.Id,
            request.Command,
            request.Arguments,
            request.RequestedBy,
            request.RequestedAt,
            decidedAt ?? DateTimeOffset.UtcNow,
            decision,
            policy,
            request.Reason);
    }
}
