using System;
using System.Collections.Generic;
using System.Management;

namespace OpenClaw.Win.Core;

public static class NodeProcessManager
{
    public static IReadOnlyList<int> FindForegroundNodeProcessIds()
    {
        var ids = new List<int>();

        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT ProcessId, Name, CommandLine FROM Win32_Process");
            using var results = searcher.Get();

            foreach (ManagementObject obj in results)
            {
                var cmd = obj["CommandLine"] as string;
                if (string.IsNullOrWhiteSpace(cmd))
                {
                    continue;
                }

                if (cmd.IndexOf("openclaw", StringComparison.OrdinalIgnoreCase) < 0)
                {
                    continue;
                }

                if (cmd.IndexOf("node", StringComparison.OrdinalIgnoreCase) < 0 ||
                    cmd.IndexOf("run", StringComparison.OrdinalIgnoreCase) < 0)
                {
                    continue;
                }

                if (obj["ProcessId"] is uint pid)
                {
                    ids.Add((int)pid);
                }
            }
        }
        catch
        {
            // best-effort only
        }

        return ids;
    }

    public static int KillForegroundNodeProcesses()
    {
        var ids = FindForegroundNodeProcessIds();
        var killed = 0;

        foreach (var id in ids)
        {
            try
            {
                var process = System.Diagnostics.Process.GetProcessById(id);
                process.Kill(true);
                killed++;
            }
            catch
            {
                // ignore
            }
        }

        return killed;
    }
}
