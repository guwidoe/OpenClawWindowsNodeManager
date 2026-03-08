using System;
using System.Collections.Generic;
using System.Linq;

namespace OpenClaw.Win.Core;

public enum CompanionTab
{
    Connection,
    NodeHost,
    Canvas,
    Approvals,
    ChromeRelay,
    Logs
}

public static class CompanionTabNames
{
    private static readonly Dictionary<string, CompanionTab> Aliases = new(StringComparer.OrdinalIgnoreCase)
    {
        ["connection"] = CompanionTab.Connection,
        ["settings"] = CompanionTab.Connection,
        ["nodehost"] = CompanionTab.NodeHost,
        ["node-host"] = CompanionTab.NodeHost,
        ["node_host"] = CompanionTab.NodeHost,
        ["node"] = CompanionTab.NodeHost,
        ["host"] = CompanionTab.NodeHost,
        ["canvas"] = CompanionTab.Canvas,
        ["approvals"] = CompanionTab.Approvals,
        ["approval"] = CompanionTab.Approvals,
        ["chrome-relay"] = CompanionTab.ChromeRelay,
        ["chrome_relay"] = CompanionTab.ChromeRelay,
        ["chromerelay"] = CompanionTab.ChromeRelay,
        ["relay"] = CompanionTab.ChromeRelay,
        ["logs"] = CompanionTab.Logs,
        ["log"] = CompanionTab.Logs
    };

    public static IReadOnlyList<CompanionTab> All { get; } = Enum.GetValues<CompanionTab>();

    public static bool TryParse(string? value, out CompanionTab tab)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            tab = default;
            return false;
        }

        var normalized = value.Trim().ToLowerInvariant();
        return Aliases.TryGetValue(normalized, out tab);
    }

    public static string ToDisplayName(CompanionTab tab)
    {
        return tab switch
        {
            CompanionTab.Connection => "Connection",
            CompanionTab.NodeHost => "Node Host",
            CompanionTab.Canvas => "Canvas",
            CompanionTab.Approvals => "Approvals",
            CompanionTab.ChromeRelay => "Chrome Relay",
            CompanionTab.Logs => "Logs",
            _ => tab.ToString()
        };
    }

    public static string ToArgument(CompanionTab tab)
    {
        return tab switch
        {
            CompanionTab.Connection => "connection",
            CompanionTab.NodeHost => "node-host",
            CompanionTab.Canvas => "canvas",
            CompanionTab.Approvals => "approvals",
            CompanionTab.ChromeRelay => "chrome-relay",
            CompanionTab.Logs => "logs",
            _ => tab.ToString().ToLowerInvariant()
        };
    }

    public static string SupportedArgumentsText()
    {
        return string.Join(", ", All.Select(ToArgument));
    }
}

public sealed record CompanionUiLaunchRequest(bool ShowSettings, CompanionTab? Tab)
{
    public static CompanionUiLaunchRequest Hidden { get; } = new(false, null);

    public static CompanionUiLaunchRequest Show(CompanionTab? tab = null)
    {
        return new CompanionUiLaunchRequest(true, tab);
    }

    public static CompanionUiLaunchRequest ParseAppArguments(string[] args)
    {
        if (args == null || args.Length == 0)
        {
            return Hidden;
        }

        var showSettings = false;
        CompanionTab? tab = null;

        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];
            if (arg.Equals("--show", StringComparison.OrdinalIgnoreCase) ||
                arg.Equals("--show-settings", StringComparison.OrdinalIgnoreCase))
            {
                showSettings = true;
                continue;
            }

            if (arg.Equals("--tab", StringComparison.OrdinalIgnoreCase) &&
                i + 1 < args.Length &&
                CompanionTabNames.TryParse(args[i + 1], out var parsedTab))
            {
                tab = parsedTab;
                showSettings = true;
                i++;
            }
        }

        return new CompanionUiLaunchRequest(showSettings, tab);
    }

    public IReadOnlyList<string> ToAppArguments()
    {
        var arguments = new List<string>();
        if (ShowSettings || Tab.HasValue)
        {
            arguments.Add("--show-settings");
        }

        if (Tab.HasValue)
        {
            arguments.Add("--tab");
            arguments.Add(CompanionTabNames.ToArgument(Tab.Value));
        }

        return arguments;
    }
}
