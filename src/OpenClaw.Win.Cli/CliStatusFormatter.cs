using System;
using System.Collections.Generic;
using OpenClaw.Win.Core;

namespace OpenClaw.Win.Cli;

public static class CliStatusFormatter
{
    public static string FormatStatus(NodeStatus status)
    {
        var lines = new List<string>
        {
            $"state: {status.ToConnectionState()}",
            $"installed: {status.IsInstalled}",
            $"running: {status.IsRunning}",
            $"connected: {status.IsConnected}"
        };

        if (!string.IsNullOrWhiteSpace(status.GatewayHost))
        {
            lines.Add($"gateway: {status.GatewayHost}:{status.GatewayPort}");
        }

        if (!string.IsNullOrWhiteSpace(status.LastError))
        {
            lines.Add($"error: {status.LastError}");
        }

        return string.Join(Environment.NewLine, lines);
    }

    public static int MapExitCode(NodeStatus status)
    {
        if (status.Issue == NodeIssue.OpenClawMissing)
        {
            return ExitCodes.OpenClawMissing;
        }

        if (status.Issue == NodeIssue.ConfigMissing)
        {
            return ExitCodes.ConfigMissing;
        }

        if (status.Issue == NodeIssue.TokenInvalid)
        {
            return ExitCodes.AuthTokenError;
        }

        if (status.Issue == NodeIssue.PairingRequired)
        {
            return ExitCodes.PairingRequired;
        }

        if (status.IsRunning && status.IsConnected)
        {
            return ExitCodes.Success;
        }

        if (status.IsRunning && !status.IsConnected)
        {
            return ExitCodes.Degraded;
        }

        if (!status.IsRunning)
        {
            return ExitCodes.Disconnected;
        }

        return ExitCodes.GenericFailure;
    }
}
