using System;

namespace OpenClaw.Win.Core;

public enum NodeConnectionState
{
    Unknown,
    Disconnected,
    Connecting,
    Connected,
    Degraded,
    Error
}

public enum NodeIssue
{
    None,
    OpenClawMissing,
    ConfigMissing,
    TokenMissing,
    TokenInvalid,
    PairingRequired,
    GatewayUnreachable,
    UnknownError
}

public sealed class NodeStatus
{
    public bool IsOpenClawAvailable { get; set; }
    public bool IsInstalled { get; set; }
    public bool IsRunning { get; set; }
    public bool IsConnected { get; set; }
    public string? GatewayHost { get; set; }
    public int? GatewayPort { get; set; }
    public string? NodeId { get; set; }
    public string? DisplayName { get; set; }
    public DateTimeOffset? LastConnectedAt { get; set; }
    public string? LastError { get; set; }
    public NodeIssue Issue { get; set; } = NodeIssue.None;
    public string? RawJson { get; set; }
    public string? RawText { get; set; }

    public NodeConnectionState ToConnectionState()
    {
        if (Issue != NodeIssue.None && Issue != NodeIssue.TokenMissing)
        {
            return NodeConnectionState.Error;
        }

        if (IsRunning && IsConnected)
        {
            return NodeConnectionState.Connected;
        }

        if (IsRunning && !IsConnected)
        {
            return NodeConnectionState.Degraded;
        }

        if (!IsRunning)
        {
            return NodeConnectionState.Disconnected;
        }

        return NodeConnectionState.Unknown;
    }
}
