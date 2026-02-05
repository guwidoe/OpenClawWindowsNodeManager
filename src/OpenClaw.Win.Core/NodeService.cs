using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace OpenClaw.Win.Core;

public sealed class NodeService
{
    private sealed record GatewayProbeResult(bool? Connected, NodeIssue? Issue, string? ErrorMessage);
    private static readonly TimeSpan StatusTimeout = TimeSpan.FromSeconds(30);

    private readonly ConfigStore _configStore;
    private readonly TokenStore _tokenStore;
    private readonly OpenClawCliLocator _cliLocator;

    public NodeService(ConfigStore configStore, TokenStore tokenStore, OpenClawCliLocator cliLocator)
    {
        _configStore = configStore;
        _tokenStore = tokenStore;
        _cliLocator = cliLocator;
    }

    public async Task<NodeStatus> GetStatusAsync(CancellationToken cancellationToken = default)
    {
        var status = new NodeStatus();
        var cliPath = await _cliLocator.FindAsync().ConfigureAwait(false);
        if (cliPath == null)
        {
            status.IsOpenClawAvailable = false;
            status.Issue = NodeIssue.OpenClawMissing;
            status.LastError = "openclaw CLI not found.";
            return status;
        }

        status.IsOpenClawAvailable = true;

        var configExists = _configStore.Exists;
        var config = _configStore.Load();
        var identity = NodeIdentity.Load(config.DisplayName);

        var gatewayHost = !string.IsNullOrWhiteSpace(config.GatewayHost)
            ? config.GatewayHost
            : identity.GatewayHost;
        var gatewayPort = !string.IsNullOrWhiteSpace(config.GatewayHost)
            ? config.GatewayPort
            : (identity.GatewayPort ?? config.GatewayPort);
        var gatewayUseTls = !string.IsNullOrWhiteSpace(config.GatewayHost)
            ? config.UseTls
            : (identity.GatewayUseTls ?? config.UseTls);

        if (!string.IsNullOrWhiteSpace(gatewayHost))
        {
            status.GatewayHost = gatewayHost;
            status.GatewayPort = gatewayPort;
        }

        if (!string.IsNullOrWhiteSpace(identity.Id))
        {
            status.NodeId = identity.Id;
        }

        if (!string.IsNullOrWhiteSpace(identity.DisplayName))
        {
            status.DisplayName = identity.DisplayName;
        }

        if (!configExists && string.IsNullOrWhiteSpace(gatewayHost))
        {
            status.Issue = NodeIssue.ConfigMissing;
        }

        if (!_tokenStore.HasToken && status.Issue == NodeIssue.None)
        {
            status.Issue = NodeIssue.TokenMissing;
        }

        var result = await RunOpenClawAsync(cliPath, "node status --json", cancellationToken, StatusTimeout).ConfigureAwait(false);
        var combined = CombineOutput(result);

        var json = TryExtractJson(combined);
        var parsed = NodeStatusParser.Parse(json, json == null ? combined : null);
        MergeStatus(status, parsed);

        if (result.TimedOut)
        {
            status.IsStatusCheckFailed = true;
            status.LastError ??= "Status check timed out.";
        }

        var foregroundIds = NodeProcessManager.FindForegroundNodeProcessIds();
        if (foregroundIds.Count > 0)
        {
            status.HasForegroundProcess = true;
            status.IsRunning = true;
        }

        if (status.IsRunning && !status.IsConnected && !string.IsNullOrWhiteSpace(gatewayHost))
        {
            var probe = await CheckConnectedViaGatewayAsync(cliPath, gatewayHost, gatewayPort, gatewayUseTls, identity, cancellationToken).ConfigureAwait(false);
            if (probe.Connected.HasValue)
            {
                status.IsConnected = probe.Connected.Value;
            }

            if (probe.Issue.HasValue && status.Issue == NodeIssue.None)
            {
                status.Issue = probe.Issue.Value;
                status.LastError ??= probe.ErrorMessage;
            }
            else if (!probe.Connected.HasValue && !_tokenStore.HasToken && status.Issue == NodeIssue.None)
            {
                status.Issue = NodeIssue.TokenMissing;
                status.LastError ??= "Gateway token required to verify connection.";
            }
        }

        if (!status.IsRunning && (status.Issue == NodeIssue.PairingRequired || status.Issue == NodeIssue.TokenInvalid))
        {
            status.Issue = NodeIssue.None;
            status.LastError = null;
        }

        if (result.ExitCode != 0 && status.Issue == NodeIssue.None && !result.TimedOut)
        {
            status.Issue = NodeIssue.UnknownError;
            status.LastError ??= string.IsNullOrWhiteSpace(result.StdErr) ? "openclaw status failed." : result.StdErr.Trim();
        }

        return status;
    }

    public async Task<NodeStatus> ConnectAsync(TimeSpan? timeout = null, CancellationToken cancellationToken = default, IProgress<string>? progress = null)
    {
        progress?.Report("Checking OpenClaw status...");
        var cliPath = await _cliLocator.FindAsync().ConfigureAwait(false);
        if (cliPath == null)
        {
            return new NodeStatus
            {
                IsOpenClawAvailable = false,
                Issue = NodeIssue.OpenClawMissing,
                LastError = "openclaw CLI not found."
            };
        }

        if (!_configStore.Exists)
        {
            return new NodeStatus
            {
                IsOpenClawAvailable = true,
                Issue = NodeIssue.ConfigMissing,
                LastError = "Config missing."
            };
        }

        var config = _configStore.Load();
        var installArgs = BuildInstallArgs(config);

        var status = await GetStatusAsync(cancellationToken).ConfigureAwait(false);
        if (!status.IsInstalled)
        {
            progress?.Report("Installing node service...");
            await RunOpenClawAsync(cliPath, installArgs, cancellationToken).ConfigureAwait(false);
        }

        progress?.Report("Restarting node service...");
        await RunOpenClawAsync(cliPath, "node restart --json", cancellationToken).ConfigureAwait(false);

        var waitTimeout = timeout ?? TimeSpan.FromSeconds(30);
        progress?.Report("Waiting for connection...");
        return await WaitForConnectedAsync(waitTimeout, cancellationToken, progress).ConfigureAwait(false);
    }

    public async Task<NodeStatus> DisconnectAsync(TimeSpan? timeout = null, CancellationToken cancellationToken = default, IProgress<string>? progress = null)
    {
        progress?.Report("Stopping node service...");
        var cliPath = await _cliLocator.FindAsync().ConfigureAwait(false);
        if (cliPath == null)
        {
            return new NodeStatus
            {
                IsOpenClawAvailable = false,
                Issue = NodeIssue.OpenClawMissing,
                LastError = "openclaw CLI not found."
            };
        }

        await RunOpenClawAsync(cliPath, "node stop --json", cancellationToken).ConfigureAwait(false);

        var waitTimeout = timeout ?? TimeSpan.FromSeconds(20);
        progress?.Report("Waiting for shutdown...");
        var status = await WaitForStoppedAsync(waitTimeout, cancellationToken, progress).ConfigureAwait(false);

        progress?.Report("Closing foreground node...");
        var killed = NodeProcessManager.KillForegroundNodeProcesses();
        if (killed > 0)
        {
            Log.Warn($"Stopped {killed} foreground OpenClaw node process(es).");
        }

        return status;
    }

    public async Task<ProcessResult> InstallAsync(CancellationToken cancellationToken = default)
    {
        var cliPath = await _cliLocator.FindAsync().ConfigureAwait(false);
        if (cliPath == null)
        {
            throw new InvalidOperationException("openclaw CLI not found.");
        }

        if (!_configStore.Exists)
        {
            throw new InvalidOperationException("Config missing.");
        }

        var config = _configStore.Load();
        return await RunOpenClawAsync(cliPath, BuildInstallArgs(config), cancellationToken).ConfigureAwait(false);
    }

    public async Task<ProcessResult> UninstallAsync(CancellationToken cancellationToken = default)
    {
        var cliPath = await _cliLocator.FindAsync().ConfigureAwait(false);
        if (cliPath == null)
        {
            throw new InvalidOperationException("openclaw CLI not found.");
        }

        return await RunOpenClawAsync(cliPath, "node uninstall --json", cancellationToken).ConfigureAwait(false);
    }

    private async Task<NodeStatus> WaitForConnectedAsync(TimeSpan timeout, CancellationToken cancellationToken, IProgress<string>? progress)
    {
        var start = DateTimeOffset.UtcNow;
        NodeStatus last = new();

        while (DateTimeOffset.UtcNow - start < timeout)
        {
            last = await GetStatusAsync(cancellationToken).ConfigureAwait(false);
            var elapsed = (int)(DateTimeOffset.UtcNow - start).TotalSeconds;
            var phase = last.IsRunning ? "Waiting for gateway connection" : "Waiting for node start";
            progress?.Report($"{phase}... ({elapsed}s)");
            if (last.IsRunning && last.IsConnected)
            {
                return last;
            }

            await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken).ConfigureAwait(false);
        }

        return last;
    }

    private async Task<NodeStatus> WaitForStoppedAsync(TimeSpan timeout, CancellationToken cancellationToken, IProgress<string>? progress)
    {
        var start = DateTimeOffset.UtcNow;
        NodeStatus last = new();

        while (DateTimeOffset.UtcNow - start < timeout)
        {
            last = await GetStatusAsync(cancellationToken).ConfigureAwait(false);
            var elapsed = (int)(DateTimeOffset.UtcNow - start).TotalSeconds;
            progress?.Report($"Waiting for node shutdown... ({elapsed}s)");
            if (!last.IsRunning)
            {
                return last;
            }

            await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken).ConfigureAwait(false);
        }

        return last;
    }

    private async Task<ProcessResult> RunOpenClawAsync(string cliPath, string arguments, CancellationToken cancellationToken, TimeSpan? timeoutOverride = null)
    {
        var env = new Dictionary<string, string?>();
        var token = _tokenStore.LoadToken();
        if (!string.IsNullOrWhiteSpace(token))
        {
            env["OPENCLAW_GATEWAY_TOKEN"] = token;
        }

        var result = await ProcessRunner.RunAsync(
            cliPath,
            arguments,
            timeoutOverride ?? TimeSpan.FromSeconds(15),
            environment: env,
            cancellationToken: cancellationToken).ConfigureAwait(false);

        AppendNodeLog(arguments, result);
        return result;
    }

    private async Task<GatewayProbeResult> CheckConnectedViaGatewayAsync(string cliPath, string gatewayHost, int gatewayPort, bool gatewayUseTls, NodeIdentity identity, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(gatewayHost))
        {
            return new GatewayProbeResult(null, null, null);
        }

        var url = BuildGatewayUrl(gatewayHost, gatewayPort, gatewayUseTls);
        if (url == null)
        {
            return new GatewayProbeResult(null, null, null);
        }

        var tokenArg = BuildTokenArg();
        var result = await RunOpenClawAsync(cliPath, $"nodes status --connected --json --url {url}{tokenArg}", cancellationToken, StatusTimeout).ConfigureAwait(false);
        var output = CombineOutput(result);

        if (result.TimedOut)
        {
            return new GatewayProbeResult(null, null, "Gateway status check timed out.");
        }

        if (result.ExitCode != 0)
        {
            if (!string.IsNullOrWhiteSpace(output))
            {
                var lowered = output.ToLowerInvariant();
                if (lowered.Contains("pairing required"))
                {
                    return new GatewayProbeResult(null, NodeIssue.PairingRequired, "Gateway pairing required.");
                }
                if (lowered.Contains("unauthorized") || lowered.Contains("token"))
                {
                    return new GatewayProbeResult(null, NodeIssue.TokenInvalid, "Gateway token invalid or missing.");
                }
            }

            return new GatewayProbeResult(null, null, null);
        }

        return new GatewayProbeResult(NodesStatusParser.ContainsNode(output, identity), null, null);
    }

    private string BuildTokenArg()
    {
        var token = _tokenStore.LoadToken();
        if (string.IsNullOrWhiteSpace(token))
        {
            return string.Empty;
        }

        return $" --token {EscapeArg(token)}";
    }

    private static string? BuildGatewayUrl(string gatewayHost, int gatewayPort, bool gatewayUseTls)
    {
        if (string.IsNullOrWhiteSpace(gatewayHost))
        {
            return null;
        }

        var scheme = gatewayUseTls ? "https" : "http";
        return $"{scheme}://{gatewayHost}:{gatewayPort}";
    }

    private static string BuildInstallArgs(AppConfig config)
    {
        var builder = new StringBuilder("node install");
        builder.Append(" --host ").Append(EscapeArg(config.GatewayHost));
        builder.Append(" --port ").Append(config.GatewayPort);

        if (config.UseTls)
        {
            builder.Append(" --tls");
        }

        if (!string.IsNullOrWhiteSpace(config.TlsFingerprint))
        {
            builder.Append(" --tls-fingerprint ").Append(EscapeArg(config.TlsFingerprint));
        }

        if (!string.IsNullOrWhiteSpace(config.DisplayName))
        {
            builder.Append(" --display-name ").Append(EscapeArg(config.DisplayName));
        }

        builder.Append(" --force");
        return builder.ToString();
    }

    private static string EscapeArg(string value)
    {
        return value.Contains(' ') ? $"\"{value}\"" : value;
    }

    private static string CombineOutput(ProcessResult result)
    {
        if (!string.IsNullOrWhiteSpace(result.StdOut))
        {
            return result.StdOut;
        }

        return result.StdErr;
    }

    private static string? TryExtractJson(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        var trimmed = text.Trim();
        if (trimmed.StartsWith("{") || trimmed.StartsWith("["))
        {
            return trimmed;
        }

        var firstBrace = trimmed.IndexOf('{');
        var lastBrace = trimmed.LastIndexOf('}');
        if (firstBrace >= 0 && lastBrace > firstBrace)
        {
            return trimmed.Substring(firstBrace, lastBrace - firstBrace + 1);
        }

        return null;
    }

    private static void MergeStatus(NodeStatus target, NodeStatus parsed)
    {
        target.IsInstalled = target.IsInstalled || parsed.IsInstalled;
        target.IsRunning = parsed.IsRunning;
        target.IsConnected = parsed.IsConnected;
        target.GatewayHost ??= parsed.GatewayHost;
        target.GatewayPort ??= parsed.GatewayPort;
        target.NodeId ??= parsed.NodeId;
        target.DisplayName ??= parsed.DisplayName;
        target.LastConnectedAt ??= parsed.LastConnectedAt;
        target.LastError ??= parsed.LastError;

        if (target.Issue == NodeIssue.None && parsed.Issue != NodeIssue.None)
        {
            target.Issue = parsed.Issue;
        }
    }

    private static void AppendNodeLog(string arguments, ProcessResult result)
    {
        AppPaths.EnsureDirectories();
        var builder = new StringBuilder();
        builder.AppendLine($"[{DateTimeOffset.Now:O}] openclaw {RedactSecrets(arguments)}");
        if (!string.IsNullOrWhiteSpace(result.StdOut))
        {
            builder.AppendLine(RedactSecrets(result.StdOut.TrimEnd()));
        }

        if (!string.IsNullOrWhiteSpace(result.StdErr))
        {
            builder.AppendLine(RedactSecrets(result.StdErr.TrimEnd()));
        }

        builder.AppendLine($"exit={result.ExitCode} timeout={result.TimedOut}");
        builder.AppendLine(new string('-', 64));

        File.AppendAllText(AppPaths.NodeLogPath, builder.ToString());
    }

    private static string RedactSecrets(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return input;
        }

        var redacted = System.Text.RegularExpressions.Regex.Replace(
            input,
            "(--token\\s+)(\\S+)",
            "$1<redacted>",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        redacted = System.Text.RegularExpressions.Regex.Replace(
            redacted,
            "(token=)([0-9a-fA-F]+)",
            "$1<redacted>",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        return redacted;
    }
}
