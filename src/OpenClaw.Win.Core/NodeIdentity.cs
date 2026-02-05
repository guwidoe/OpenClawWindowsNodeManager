using System;
using System.IO;
using System.Text.Json;

namespace OpenClaw.Win.Core;

public sealed class NodeIdentity
{
    public string? Id { get; init; }
    public string? DisplayName { get; init; }
    public string? GatewayHost { get; init; }
    public int? GatewayPort { get; init; }
    public bool? GatewayUseTls { get; init; }

    public static NodeIdentity Load(string? displayNameFallback = null)
    {
        var stateDir = Environment.GetEnvironmentVariable("OPENCLAW_STATE_DIR");
        if (string.IsNullOrWhiteSpace(stateDir))
        {
            stateDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".openclaw");
        }

        var nodePath = Path.Combine(stateDir, "node.json");
        if (!File.Exists(nodePath))
        {
            return new NodeIdentity { DisplayName = displayNameFallback };
        }

        try
        {
            using var stream = File.OpenRead(nodePath);
            using var doc = JsonDocument.Parse(stream);
            var root = doc.RootElement;

            var id = root.TryGetProperty("id", out var idProp) ? idProp.GetString() : null;
            if (string.IsNullOrWhiteSpace(id) && root.TryGetProperty("nodeId", out var nodeIdProp))
            {
                id = nodeIdProp.GetString();
            }

            var name = root.TryGetProperty("displayName", out var nameProp) ? nameProp.GetString() : null;
            string? gatewayHost = null;
            int? gatewayPort = null;
            bool? gatewayTls = null;

            if (root.TryGetProperty("gateway", out var gateway) && gateway.ValueKind == JsonValueKind.Object)
            {
                gatewayHost = gateway.TryGetProperty("host", out var hostProp) ? hostProp.GetString() : null;
                if (gateway.TryGetProperty("port", out var portProp) && portProp.ValueKind == JsonValueKind.Number && portProp.TryGetInt32(out var port))
                {
                    gatewayPort = port;
                }

                if (gateway.TryGetProperty("tls", out var tlsProp) && tlsProp.ValueKind is JsonValueKind.True or JsonValueKind.False)
                {
                    gatewayTls = tlsProp.GetBoolean();
                }
            }

            return new NodeIdentity
            {
                Id = id,
                DisplayName = string.IsNullOrWhiteSpace(name) ? displayNameFallback : name,
                GatewayHost = gatewayHost,
                GatewayPort = gatewayPort,
                GatewayUseTls = gatewayTls
            };
        }
        catch
        {
            return new NodeIdentity { DisplayName = displayNameFallback };
        }
    }
}
