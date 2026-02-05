using System;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace OpenClaw.Win.Core;

public sealed class SshTokenFetcher
{
    public async Task<SshTokenFetchResult> FetchAsync(string host, string user, int port, string command, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(host) || string.IsNullOrWhiteSpace(user))
        {
            return new SshTokenFetchResult
            {
                Success = false,
                ErrorMessage = "SSH host and user are required."
            };
        }

        if (string.IsNullOrWhiteSpace(command))
        {
            return new SshTokenFetchResult
            {
                Success = false,
                ErrorMessage = "SSH command is required."
            };
        }

        var args = new StringBuilder();
        args.Append("-o BatchMode=yes -o ConnectTimeout=10 ");
        if (port > 0)
        {
            args.Append("-p ").Append(port).Append(' ');
        }

        args.Append(user).Append('@').Append(host).Append(' ');
        args.Append('"').Append(EscapeSshCommand(command)).Append('"');

        var result = await ProcessRunner.RunAsync(
            "ssh",
            args.ToString(),
            TimeSpan.FromSeconds(20),
            cancellationToken: cancellationToken).ConfigureAwait(false);

        var output = string.Join("\n", new[] { result.StdOut, result.StdErr });
        var tokenMatch = Regex.Match(output, "token=([a-zA-Z0-9]+)");
        if (tokenMatch.Success)
        {
            return new SshTokenFetchResult
            {
                Success = true,
                Token = tokenMatch.Groups[1].Value,
                Output = RedactToken(output)
            };
        }

        if (result.ExitCode != 0)
        {
            return new SshTokenFetchResult
            {
                Success = false,
                ErrorMessage = "SSH command failed. Ensure key-based auth is set up.",
                Output = RedactToken(output)
            };
        }

        return new SshTokenFetchResult
        {
            Success = false,
            ErrorMessage = "Token not found in SSH output.",
            Output = RedactToken(output)
        };
    }

    private static string EscapeSshCommand(string command)
    {
        return command.Replace("\"", "\\\"");
    }

    private static string RedactToken(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return input;
        }

        return Regex.Replace(input, "(token=)([a-zA-Z0-9]+)", "$1<redacted>", RegexOptions.IgnoreCase);
    }
}
