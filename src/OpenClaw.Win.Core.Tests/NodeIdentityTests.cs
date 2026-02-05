using System;
using System.IO;
using OpenClaw.Win.Core;
using Xunit;

namespace OpenClaw.Win.Core.Tests;

public class NodeIdentityTests
{
    [Fact]
    public void Load_UsesStateDirWhenProvided()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        var nodePath = Path.Combine(tempDir, "node.json");
        File.WriteAllText(nodePath, """
                                   {
                                     "nodeId": "node-789",
                                     "displayName": "Guido-Node",
                                     "gateway": { "host": "gw.local", "port": 443, "tls": true }
                                   }
                                   """);

        var original = Environment.GetEnvironmentVariable("OPENCLAW_STATE_DIR");
        try
        {
            Environment.SetEnvironmentVariable("OPENCLAW_STATE_DIR", tempDir);
            var identity = NodeIdentity.Load();

            Assert.Equal("node-789", identity.Id);
            Assert.Equal("Guido-Node", identity.DisplayName);
            Assert.Equal("gw.local", identity.GatewayHost);
            Assert.Equal(443, identity.GatewayPort);
            Assert.True(identity.GatewayUseTls);
        }
        finally
        {
            Environment.SetEnvironmentVariable("OPENCLAW_STATE_DIR", original);
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, recursive: true);
            }
        }
    }

    [Fact]
    public void Load_UsesDisplayNameFallbackWhenNoFile()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        var original = Environment.GetEnvironmentVariable("OPENCLAW_STATE_DIR");
        try
        {
            Environment.SetEnvironmentVariable("OPENCLAW_STATE_DIR", tempDir);
            var identity = NodeIdentity.Load("Fallback-Name");

            Assert.Equal("Fallback-Name", identity.DisplayName);
            Assert.Null(identity.Id);
        }
        finally
        {
            Environment.SetEnvironmentVariable("OPENCLAW_STATE_DIR", original);
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, recursive: true);
            }
        }
    }
}
