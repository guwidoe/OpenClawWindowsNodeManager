using System;
using System.Diagnostics;
using System.IO;

namespace OpenClaw.Win.Core;

public sealed class HiddenNodeHostRunner : INodeHostRunner
{
    private static readonly string NodeCmdPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        ".openclaw",
        "node.cmd");

    private readonly object _processLock = new();
    private Process? _process;

    public bool IsRunning
    {
        get
        {
            lock (_processLock)
            {
                return _process != null && !_process.HasExited;
            }
        }
    }

    public bool Start(string cliPath, string runArgs, string? token)
    {
        try
        {
            var openclawMjs = ResolveOpenClawMjsPath(cliPath);
            var (fileName, arguments) = openclawMjs == null
                ? BuildCliInvocation(cliPath, runArgs)
                : (ResolveNodeExePath(), $"\"{openclawMjs}\" {runArgs}");

            var startInfo = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden
            };

            if (!string.IsNullOrWhiteSpace(token))
            {
                startInfo.Environment["OPENCLAW_GATEWAY_TOKEN"] = token;
            }

            var process = new Process
            {
                StartInfo = startInfo,
                EnableRaisingEvents = true
            };

            if (!process.Start())
            {
                return false;
            }

            AttachNodeHostLogging(process, fileName, arguments);

            lock (_processLock)
            {
                _process = process;
            }

            return true;
        }
        catch (Exception ex)
        {
            Log.Error("Failed to start hidden node host.", ex);
            return false;
        }
    }

    public void Stop()
    {
        lock (_processLock)
        {
            if (_process == null)
            {
                return;
            }

            try
            {
                if (!_process.HasExited)
                {
                    _process.Kill(true);
                }
            }
            catch
            {
                // ignore
            }
            finally
            {
                _process = null;
            }
        }
    }

    private static void AttachNodeHostLogging(Process process, string fileName, string arguments)
    {
        AppPaths.EnsureDirectories();
        var logPath = AppPaths.NodeHostLogPath;

        void AppendLine(string prefix, string line)
        {
            var entry = $"[{DateTimeOffset.Now:O}] {prefix}{line}{Environment.NewLine}";
            File.AppendAllText(logPath, entry);
        }

        AppendLine(string.Empty, $"START {fileName} {arguments}");

        process.OutputDataReceived += (_, args) =>
        {
            if (!string.IsNullOrWhiteSpace(args.Data))
            {
                AppendLine(string.Empty, args.Data);
            }
        };

        process.ErrorDataReceived += (_, args) =>
        {
            if (!string.IsNullOrWhiteSpace(args.Data))
            {
                AppendLine("ERR: ", args.Data);
            }
        };

        process.Exited += (_, _) =>
        {
            AppendLine(string.Empty, $"[node host exited with code {process.ExitCode}]");
        };

        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
    }

    private static (string FileName, string Arguments) BuildCliInvocation(string cliPath, string arguments)
    {
        var lower = cliPath.ToLowerInvariant();
        if (lower.EndsWith(".cmd") || lower.EndsWith(".bat"))
        {
            return ("cmd.exe", $"/c \"{cliPath}\" {arguments}");
        }
        if (lower.EndsWith(".ps1"))
        {
            return ("powershell.exe", $"-NoProfile -ExecutionPolicy Bypass -File \"{cliPath}\" {arguments}");
        }

        return (cliPath, arguments);
    }

    private static string? ResolveOpenClawMjsPath(string cliPath)
    {
        var directory = Path.GetDirectoryName(cliPath);
        if (string.IsNullOrWhiteSpace(directory))
        {
            return null;
        }

        var mjsPath = Path.Combine(directory, "node_modules", "openclaw", "openclaw.mjs");
        return File.Exists(mjsPath) ? mjsPath : null;
    }

    private static string ResolveNodeExePath()
    {
        var fromCmd = TryResolveNodeExeFromNodeCmd();
        if (!string.IsNullOrWhiteSpace(fromCmd))
        {
            return fromCmd!;
        }

        return "node";
    }

    private static string? TryResolveNodeExeFromNodeCmd()
    {
        if (!File.Exists(NodeCmdPath))
        {
            return null;
        }

        foreach (var line in File.ReadAllLines(NodeCmdPath))
        {
            var trimmed = line.Trim();
            if (trimmed.Length == 0)
            {
                continue;
            }

            var match = System.Text.RegularExpressions.Regex.Match(
                trimmed,
                "\"([^\"]*node\\.exe)\"",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            if (match.Success)
            {
                var path = match.Groups[1].Value;
                if (File.Exists(path))
                {
                    return path;
                }
            }
        }

        return null;
    }
}
