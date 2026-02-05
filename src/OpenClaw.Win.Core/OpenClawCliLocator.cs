using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace OpenClaw.Win.Core;

public sealed class OpenClawCliLocator : IOpenClawCliLocator
{
    private static readonly string[] ExecutableNames =
    {
        "openclaw.exe",
        "openclaw.cmd",
        "openclaw.bat",
        "openclaw.ps1",
        "openclaw"
    };

    public async Task<string?> FindAsync()
    {
        var envOverride = Environment.GetEnvironmentVariable("OPENCLAW_CLI_PATH");
        if (!string.IsNullOrWhiteSpace(envOverride) && File.Exists(envOverride))
        {
            return envOverride;
        }

        var npmPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "npm");

        var commonDirs = new List<string>
        {
            npmPath
        };

        var pathVar = Environment.GetEnvironmentVariable("PATH");
        if (!string.IsNullOrWhiteSpace(pathVar))
        {
            commonDirs.AddRange(pathVar.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries));
        }

        foreach (var dir in commonDirs.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            foreach (var name in ExecutableNames)
            {
                var candidate = Path.Combine(dir, name);
                if (File.Exists(candidate))
                {
                    return candidate;
                }
            }
        }

        var whereResult = await TryWhereAsync().ConfigureAwait(false);
        return whereResult;
    }

    private static async Task<string?> TryWhereAsync()
    {
        try
        {
            var result = await ProcessRunner.RunAsync("where", "openclaw", TimeSpan.FromSeconds(3)).ConfigureAwait(false);
            if (result.ExitCode == 0)
            {
                var firstLine = result.StdOut.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
                if (!string.IsNullOrWhiteSpace(firstLine))
                {
                    return firstLine.Trim();
                }
            }
        }
        catch
        {
            // ignore
        }

        return null;
    }
}
