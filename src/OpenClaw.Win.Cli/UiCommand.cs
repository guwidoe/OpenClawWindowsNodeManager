using System;
using OpenClaw.Win.Core;

namespace OpenClaw.Win.Cli;

public sealed record UiCommandOptions(bool ListTabs, CompanionUiLaunchRequest LaunchRequest)
{
    public static UiCommandOptions Show(CompanionTab? tab = null) => new(false, CompanionUiLaunchRequest.Show(tab));
    public static UiCommandOptions Tabs() => new(true, CompanionUiLaunchRequest.Hidden);
}

public static class UiCommandParser
{
    public static bool TryParse(string[] args, out UiCommandOptions options, out string? error)
    {
        options = UiCommandOptions.Show();
        error = null;

        if (args.Length == 0)
        {
            return true;
        }

        var first = args[0].ToLowerInvariant();
        if (first == "tabs")
        {
            if (args.Length > 1)
            {
                error = "The 'ui tabs' command does not accept extra arguments.";
                return false;
            }

            options = UiCommandOptions.Tabs();
            return true;
        }

        var offset = 0;
        if (first == "show")
        {
            offset = 1;
        }
        else if (first.StartsWith("--", StringComparison.Ordinal))
        {
            offset = 0;
        }
        else
        {
            error = $"Unknown ui subcommand: {args[0]}";
            return false;
        }

        CompanionTab? tab = null;
        for (var i = offset; i < args.Length; i++)
        {
            var arg = args[i];
            if (arg.Equals("--tab", StringComparison.OrdinalIgnoreCase))
            {
                if (i + 1 >= args.Length)
                {
                    error = "Missing value for --tab.";
                    return false;
                }

                if (!CompanionTabNames.TryParse(args[i + 1], out var parsedTab))
                {
                    error = $"Unknown tab '{args[i + 1]}'. Supported tabs: {CompanionTabNames.SupportedArgumentsText()}";
                    return false;
                }

                tab = parsedTab;
                i++;
                continue;
            }

            error = $"Unknown ui option: {arg}";
            return false;
        }

        options = UiCommandOptions.Show(tab);
        return true;
    }
}
