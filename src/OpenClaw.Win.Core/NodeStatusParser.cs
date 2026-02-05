using System;
using System.Collections.Generic;
using System.Text.Json;

namespace OpenClaw.Win.Core;

public static class NodeStatusParser
{
    public static NodeStatus Parse(string? json, string? text)
    {
        var status = new NodeStatus
        {
            RawJson = json,
            RawText = text
        };

        if (!string.IsNullOrWhiteSpace(json))
        {
            TryApplyJson(status, json!);
        }

        if (!string.IsNullOrWhiteSpace(text))
        {
            ApplyTextHeuristics(status, text!);
        }

        return status;
    }

    private static void TryApplyJson(NodeStatus status, string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (TryFindBool(root, out var installed, "installed", "serviceInstalled", "isInstalled"))
            {
                status.IsInstalled = installed;
            }

            if (TryFindBool(root, out var running, "running", "serviceRunning", "isRunning"))
            {
                status.IsRunning = running;
            }

            if (TryFindBool(root, out var connected, "connected", "isConnected", "gatewayConnected"))
            {
                status.IsConnected = connected;
            }

            if (TryFindString(root, out var state, "state", "status"))
            {
                var lowered = state!.ToLowerInvariant();
                if (lowered.Contains("connected"))
                {
                    status.IsConnected = true;
                }
                if (lowered.Contains("running") || lowered.Contains("online"))
                {
                    status.IsRunning = true;
                }
                if (lowered.Contains("stopped") || lowered.Contains("offline") || lowered.Contains("disconnected"))
                {
                    status.IsRunning = false;
                }
            }

            if (TryFindString(root, out var nodeId, "nodeId", "node_id", "id"))
            {
                status.NodeId = nodeId;
            }

            if (TryFindString(root, out var displayName, "displayName", "display_name", "name"))
            {
                status.DisplayName = displayName;
            }

            if (TryFindString(root, out var gatewayHost, "gatewayHost", "host"))
            {
                status.GatewayHost = gatewayHost;
            }

            if (TryFindInt(root, out var gatewayPort, "gatewayPort", "port"))
            {
                status.GatewayPort = gatewayPort;
            }

            if (TryFindString(root, out var lastError, "lastError", "error", "reason"))
            {
                status.LastError = lastError;
            }

            if (TryFindString(root, out var lastConnected, "lastConnectedAt", "lastConnected", "lastConnectTime"))
            {
                if (DateTimeOffset.TryParse(lastConnected, out var parsed))
                {
                    status.LastConnectedAt = parsed;
                }
            }
        }
        catch
        {
            // Ignore JSON parse errors; fall back to text parsing.
        }
    }

    private static void ApplyTextHeuristics(NodeStatus status, string text)
    {
        var lowered = text.ToLowerInvariant();

        if (lowered.Contains("running") || lowered.Contains("online"))
        {
            status.IsRunning = true;
        }

        if (lowered.Contains("stopped") || lowered.Contains("not running") || lowered.Contains("offline"))
        {
            status.IsRunning = false;
        }

        if (lowered.Contains("connected"))
        {
            status.IsConnected = true;
        }

        if (lowered.Contains("disconnected"))
        {
            status.IsConnected = false;
        }

        if (lowered.Contains("unauthorized") || lowered.Contains("token"))
        {
            status.Issue = NodeIssue.TokenInvalid;
            status.LastError ??= "Authentication failed.";
        }

        if (lowered.Contains("pair") || lowered.Contains("approve") || lowered.Contains("pending"))
        {
            status.Issue = NodeIssue.PairingRequired;
            status.LastError ??= "Pairing required.";
        }

        if (lowered.Contains("econnrefused") || lowered.Contains("unreachable") || lowered.Contains("enotfound"))
        {
            status.Issue = NodeIssue.GatewayUnreachable;
            status.LastError ??= "Gateway unreachable.";
        }
    }

    private static bool TryFindBool(JsonElement element, out bool value, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (TryFindProperty(element, key, out var prop))
            {
                if (prop.Value.ValueKind == JsonValueKind.True || prop.Value.ValueKind == JsonValueKind.False)
                {
                    value = prop.Value.GetBoolean();
                    return true;
                }

                if (prop.Value.ValueKind == JsonValueKind.String && bool.TryParse(prop.Value.GetString(), out var parsed))
                {
                    value = parsed;
                    return true;
                }
            }
        }

        value = false;
        return false;
    }

    private static bool TryFindInt(JsonElement element, out int value, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (TryFindProperty(element, key, out var prop))
            {
                if (prop.Value.ValueKind == JsonValueKind.Number && prop.Value.TryGetInt32(out var parsed))
                {
                    value = parsed;
                    return true;
                }

                if (prop.Value.ValueKind == JsonValueKind.String && int.TryParse(prop.Value.GetString(), out parsed))
                {
                    value = parsed;
                    return true;
                }
            }
        }

        value = 0;
        return false;
    }

    private static bool TryFindString(JsonElement element, out string? value, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (TryFindProperty(element, key, out var prop))
            {
                if (prop.Value.ValueKind == JsonValueKind.String)
                {
                    value = prop.Value.GetString();
                    return !string.IsNullOrWhiteSpace(value);
                }
            }
        }

        value = null;
        return false;
    }

    private static bool TryFindProperty(JsonElement element, string key, out JsonProperty prop)
    {
        foreach (var candidate in EnumerateProperties(element))
        {
            if (string.Equals(candidate.Name, key, StringComparison.OrdinalIgnoreCase))
            {
                prop = candidate;
                return true;
            }
        }

        prop = default;
        return false;
    }

    private static IEnumerable<JsonProperty> EnumerateProperties(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var prop in element.EnumerateObject())
            {
                yield return prop;

                foreach (var nested in EnumerateProperties(prop.Value))
                {
                    yield return nested;
                }
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray())
            {
                foreach (var nested in EnumerateProperties(item))
                {
                    yield return nested;
                }
            }
        }
    }
}
