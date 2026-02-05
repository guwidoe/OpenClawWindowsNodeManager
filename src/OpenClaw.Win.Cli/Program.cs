using System;
using System.Collections.Generic;
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
            config.UseDarkTheme = true;
        }

        if (options.ContainsKey("light-theme"))
        {
            config.UseDarkTheme = false;
        }

        if (options.ContainsKey("tray-notifications"))
        {
            config.EnableTrayNotifications = true;
        }

        if (options.ContainsKey("no-tray-notifications"))
        {
            config.EnableTrayNotifications = false;
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
        Console.WriteLine("  configure --host <h> --port <p> [--tls|--no-tls] [--token <t>] [--display-name <n>] [--tls-fingerprint <sha256>] [--relay-port <p>] [--control-ui <url>] [--dark-theme|--light-theme] [--tray-notifications|--no-tray-notifications]");
        Console.WriteLine("  install");
        Console.WriteLine("  uninstall");
        Console.WriteLine("  logs --tail [--lines N]");
        Console.WriteLine("  doctor");
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
}
