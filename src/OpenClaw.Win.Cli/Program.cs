using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using OpenClaw.Win.Core;
using OpenClaw.Win.Cli;

public static class Program
{
    public static async Task<int> Main(string[] args)
    {
        if (args.Length == 0 || args.Contains("--help") || args.Contains("-h"))
        {
            PrintHelp();
            return ExitCodes.GenericFailure;
        }

        var command = args[0].ToLowerInvariant();
        var options = args.Skip(1).ToArray();

        var configStore = new ConfigStore();
        var tokenStore = new TokenStore();
        var locator = new OpenClawCliLocator();
        var nodeService = new NodeService(configStore, tokenStore, locator);

        try
        {
            return command switch
            {
                "status" => await HandleStatusAsync(nodeService),
                "connect" => await HandleConnectAsync(nodeService),
                "disconnect" => await HandleDisconnectAsync(nodeService),
                "toggle" => await HandleToggleAsync(nodeService),
                "configure" => HandleConfigure(configStore, tokenStore, options),
                "install" => await HandleInstallAsync(nodeService),
                "uninstall" => await HandleUninstallAsync(nodeService),
                "logs" => HandleLogs(options),
                "doctor" => await HandleDoctorAsync(nodeService, configStore, tokenStore, locator),
                "ui" => HandleUi(options),
                _ => UnknownCommand(command)
            };
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error: {ex.Message}");
            Log.Error("CLI command failed.", ex);
            return ExitCodes.GenericFailure;
        }
    }

    private static async Task<int> HandleStatusAsync(NodeService nodeService)
    {
        var status = await nodeService.GetStatusAsync();
        PrintStatus(status);
        return MapExitCode(status);
    }

    private static async Task<int> HandleConnectAsync(NodeService nodeService)
    {
        var status = await nodeService.ConnectAsync();
        PrintStatus(status);
        return MapExitCode(status);
    }

    private static async Task<int> HandleDisconnectAsync(NodeService nodeService)
    {
        var status = await nodeService.DisconnectAsync();
        PrintStatus(status);
        return MapExitCode(status);
    }

    private static async Task<int> HandleToggleAsync(NodeService nodeService)
    {
        var status = await nodeService.GetStatusAsync();
        if (status.IsRunning)
        {
            status = await nodeService.DisconnectAsync();
        }
        else
        {
            status = await nodeService.ConnectAsync();
        }

        PrintStatus(status);
        return MapExitCode(status);
    }

    private static int HandleConfigure(ConfigStore configStore, TokenStore tokenStore, string[] args)
    {
        var options = ParseOptions(args);
        var config = configStore.Load();

        if (options.TryGetValue("host", out var host))
        {
            config.GatewayHost = host;
        }

        if (options.TryGetValue("port", out var portValue) && int.TryParse(portValue, out var port))
        {
            config.GatewayPort = port;
        }

        if (options.ContainsKey("tls"))
        {
            config.UseTls = true;
        }

        if (options.ContainsKey("no-tls"))
        {
            config.UseTls = false;
        }

        if (options.TryGetValue("tls-fingerprint", out var fingerprint))
        {
            config.TlsFingerprint = fingerprint;
        }

        if (options.TryGetValue("display-name", out var displayName))
        {
            config.DisplayName = displayName;
        }

        if (options.TryGetValue("control-ui", out var controlUi))
        {
            config.ControlUiUrl = controlUi;
        }

        if (options.TryGetValue("relay-port", out var relayPort) && int.TryParse(relayPort, out var parsedRelay))
        {
            config.RelayPort = parsedRelay;
        }

        if (options.TryGetValue("token", out var token))
        {
            tokenStore.SaveToken(token);
        }

        if (options.ContainsKey("dark-theme"))
        {
            config.ThemePreference = ThemePreference.Dark;
        }

        if (options.ContainsKey("light-theme"))
        {
            config.ThemePreference = ThemePreference.Light;
        }

        if (options.ContainsKey("system-theme"))
        {
            config.ThemePreference = ThemePreference.System;
        }

        if (options.ContainsKey("tray-notifications"))
        {
            config.EnableTrayNotifications = true;
        }

        if (options.ContainsKey("no-tray-notifications"))
        {
            config.EnableTrayNotifications = false;
        }

        if (options.ContainsKey("system-notifications"))
        {
            config.EnableSystemNotifications = true;
        }

        if (options.ContainsKey("no-system-notifications"))
        {
            config.EnableSystemNotifications = false;
        }

        if (options.TryGetValue("exec-policy", out var execPolicy))
        {
            if (TryParseExecPolicy(execPolicy, out var parsedPolicy))
            {
                config.ExecApprovalPolicy = parsedPolicy;
            }
            else
            {
                Console.Error.WriteLine("Invalid exec policy. Use prompt, allow, or deny.");
                return ExitCodes.GenericFailure;
            }
        }

        configStore.Save(config);
        Console.WriteLine("Configuration saved.");
        return ExitCodes.Success;
    }

    private static async Task<int> HandleInstallAsync(NodeService nodeService)
    {
        await nodeService.InstallAsync();
        Console.WriteLine("Install command issued.");
        return ExitCodes.Success;
    }

    private static async Task<int> HandleUninstallAsync(NodeService nodeService)
    {
        await nodeService.UninstallAsync();
        Console.WriteLine("Uninstall command issued.");
        return ExitCodes.Success;
    }

    private static int HandleLogs(string[] args)
    {
        var options = ParseOptions(args);
        var lines = 200;
        if (options.TryGetValue("lines", out var linesValue) && int.TryParse(linesValue, out var parsed))
        {
            lines = parsed;
        }

        var path = AppPaths.NodeLogPath;
        if (!File.Exists(path))
        {
            Console.WriteLine("No logs found.");
            return ExitCodes.GenericFailure;
        }

        var tail = ReadTail(path, lines);
        foreach (var line in tail)
        {
            Console.WriteLine(line);
        }

        if (options.ContainsKey("tail"))
        {
            Follow(path);
        }

        return ExitCodes.Success;
    }

    private static async Task<int> HandleDoctorAsync(NodeService nodeService, ConfigStore configStore, TokenStore tokenStore, OpenClawCliLocator locator)
    {
        Console.WriteLine("OpenClaw Windows Companion doctor");
        Console.WriteLine(new string('-', 40));

        var cliPath = await locator.FindAsync();
        if (cliPath == null)
        {
            Console.WriteLine("openclaw: MISSING (install via npm or from the OpenClaw docs)");
            return ExitCodes.OpenClawMissing;
        }

        Console.WriteLine($"openclaw: {cliPath}");

        if (!configStore.Exists)
        {
            Console.WriteLine("config: missing (run configure)");
            return ExitCodes.ConfigMissing;
        }

        var config = configStore.Load();
        Console.WriteLine($"gateway: {config.GatewayHost}:{config.GatewayPort} tls={(config.UseTls ? "on" : "off")}");

        if (!tokenStore.HasToken)
        {
            Console.WriteLine("token: missing (set --token)");
        }
        else
        {
            Console.WriteLine("token: stored (DPAPI)");
        }

        var status = await nodeService.GetStatusAsync();
        PrintStatus(status);
        return MapExitCode(status);
    }

    private static int HandleUi(string[] args)
    {
        if (!UiCommandParser.TryParse(args, out var options, out var error))
        {
            Console.Error.WriteLine(error);
            PrintUiHelp();
            return ExitCodes.GenericFailure;
        }

        if (options.ListTabs)
        {
            foreach (var supportedTab in CompanionTabNames.All)
            {
                Console.WriteLine($"- {CompanionTabNames.ToArgument(supportedTab)} ({CompanionTabNames.ToDisplayName(supportedTab)})");
            }

            return ExitCodes.Success;
        }

        var appPath = FindAppPath();
        if (appPath == null)
        {
            Console.Error.WriteLine("OpenClaw.Win.App.exe not found. Build the app or set OPENCLAW_APP_PATH.");
            return ExitCodes.GenericFailure;
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = appPath,
            UseShellExecute = true,
            WorkingDirectory = Path.GetDirectoryName(appPath) ?? AppContext.BaseDirectory
        };

        foreach (var arg in options.LaunchRequest.ToAppArguments())
        {
            startInfo.ArgumentList.Add(arg);
        }

        Process.Start(startInfo);
        var target = options.LaunchRequest.Tab is { } tab
            ? $" ({CompanionTabNames.ToDisplayName(tab)})"
            : string.Empty;
        Console.WriteLine($"Opened OpenClaw UI{target}.");
        return ExitCodes.Success;
    }

    private static string? FindAppPath()
    {
        var envOverride = Environment.GetEnvironmentVariable("OPENCLAW_APP_PATH");
        if (!string.IsNullOrWhiteSpace(envOverride) && File.Exists(envOverride))
        {
            return envOverride;
        }

        var baseDir = AppContext.BaseDirectory;
        var candidates = new[]
        {
            Path.Combine(baseDir, "OpenClaw.Win.App.exe"),
            Path.GetFullPath(Path.Combine(baseDir, "..", "..", "OpenClaw.Win.App", "release", "OpenClaw.Win.App.exe")),
            Path.GetFullPath(Path.Combine(baseDir, "..", "..", "OpenClaw.Win.App", "debug", "OpenClaw.Win.App.exe")),
            Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", "src", "OpenClaw.Win.App", "bin", "Release", "net8.0-windows", "OpenClaw.Win.App.exe")),
            Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", "src", "OpenClaw.Win.App", "bin", "Debug", "net8.0-windows", "OpenClaw.Win.App.exe"))
        };

        return candidates.FirstOrDefault(File.Exists);
    }

    private static int UnknownCommand(string command)
    {
        Console.Error.WriteLine($"Unknown command: {command}");
        PrintHelp();
        return ExitCodes.GenericFailure;
    }

    private static void PrintHelp()
    {
        Console.WriteLine("OpenClaw Windows Companion CLI");
        Console.WriteLine("Commands:");
        Console.WriteLine("  status");
        Console.WriteLine("  connect");
        Console.WriteLine("  disconnect");
        Console.WriteLine("  toggle");
        Console.WriteLine("  configure --host <h> --port <p> [--tls|--no-tls] [--token <t>] [--display-name <n>] [--tls-fingerprint <sha256>] [--relay-port <p>] [--control-ui <url>] [--dark-theme|--light-theme|--system-theme] [--tray-notifications|--no-tray-notifications] [--system-notifications|--no-system-notifications] [--exec-policy <prompt|allow|deny>]");
        Console.WriteLine("  install");
        Console.WriteLine("  uninstall");
        Console.WriteLine("  logs --tail [--lines N]");
        Console.WriteLine("  doctor");
        Console.WriteLine("  ui show [--tab <connection|node-host|canvas|approvals|chrome-relay|logs>]");
        Console.WriteLine("  ui tabs");
    }

    private static void PrintUiHelp()
    {
        Console.WriteLine("UI commands:");
        Console.WriteLine("  ui show");
        Console.WriteLine("  ui show --tab <tab>");
        Console.WriteLine("  ui tabs");
    }

    private static void PrintStatus(NodeStatus status)
    {
        Console.WriteLine(CliStatusFormatter.FormatStatus(status));
    }

    private static int MapExitCode(NodeStatus status)
    {
        return CliStatusFormatter.MapExitCode(status);
    }

    private static Dictionary<string, string> ParseOptions(string[] args)
    {
        var options = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];
            if (!arg.StartsWith("--", StringComparison.Ordinal))
            {
                continue;
            }

            var key = arg[2..];
            if (i + 1 < args.Length && !args[i + 1].StartsWith("--", StringComparison.Ordinal))
            {
                options[key] = args[i + 1];
                i++;
            }
            else
            {
                options[key] = "true";
            }
        }

        return options;
    }

    private static IEnumerable<string> ReadTail(string path, int lines)
    {
        var allLines = File.ReadAllLines(path);
        return allLines.Skip(Math.Max(0, allLines.Length - lines));
    }

    private static void Follow(string path)
    {
        using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var reader = new StreamReader(fs);
        reader.BaseStream.Seek(0, SeekOrigin.End);

        while (true)
        {
            var line = reader.ReadLine();
            if (line != null)
            {
                Console.WriteLine(line);
            }
            else
            {
                Thread.Sleep(500);
            }
        }
    }

    private static bool TryParseExecPolicy(string value, out ExecApprovalPolicy policy)
    {
        if (Enum.TryParse(value, true, out ExecApprovalPolicy parsed))
        {
            policy = parsed;
            return true;
        }

        policy = ExecApprovalPolicy.Prompt;
        return false;
    }
}
