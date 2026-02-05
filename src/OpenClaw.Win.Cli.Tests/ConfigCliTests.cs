using System;
using System.IO;
using System.Threading.Tasks;
using OpenClaw.Win.Core;
using Xunit;

namespace OpenClaw.Win.Cli.Tests;

public class ConfigCliTests
{
    [Fact]
    public async Task Configure_SetsThemeAndNotificationFlags()
    {
        using var temp = new TempStateDir();
        var exit = await Program.Main(new[] { "configure", "--dark-theme", "--no-tray-notifications", "--no-system-notifications", "--exec-policy", "deny" });

        Assert.Equal(ExitCodes.Success, exit);

        var config = new ConfigStore().Load();
        Assert.True(config.UseDarkTheme);
        Assert.False(config.EnableTrayNotifications);
        Assert.False(config.EnableSystemNotifications);
        Assert.Equal(ExecApprovalPolicy.Deny, config.ExecApprovalPolicy);
    }

    private sealed class TempStateDir : IDisposable
    {
        private const string OverrideEnvVar = "OPENCLAW_COMPANION_STATE_DIR";
        private readonly string? _previous;

        public string Path { get; }

        public TempStateDir()
        {
            _previous = Environment.GetEnvironmentVariable(OverrideEnvVar);
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path);
            Environment.SetEnvironmentVariable(OverrideEnvVar, Path);
        }

        public void Dispose()
        {
            Environment.SetEnvironmentVariable(OverrideEnvVar, _previous);
            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, recursive: true);
            }
        }
    }
}
