namespace OpenClaw.Win.Core;

public enum SystemNotificationKind
{
    StatusChange,
    ApprovalRequested,
    ApprovalApproved,
    ApprovalDenied,
    Error
}

public sealed record SystemNotificationEvent(
    SystemNotificationKind Kind,
    string Title,
    string Message,
    string? Key = null);

public sealed record SystemNotification(
    string Title,
    string Message,
    SystemNotificationKind Kind,
    string Key);
