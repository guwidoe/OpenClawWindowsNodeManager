using System;
using System.IO;

namespace OpenClaw.Win.Core;

public static class Log
{
    private static readonly object Gate = new();

    public static void Info(string message) => Write("INFO", message);

    public static void Warn(string message) => Write("WARN", message);

    public static void Error(string message, Exception? ex = null)
    {
        var details = ex == null ? message : $"{message} :: {ex.GetType().Name}: {ex.Message}";
        Write("ERROR", details);
    }

    public static void Write(string level, string message)
    {
        AppPaths.EnsureDirectories();
        var line = $"{DateTimeOffset.Now:O} [{level}] {message}";
        lock (Gate)
        {
            File.AppendAllText(AppPaths.AppLogPath, line + Environment.NewLine);
        }
    }
}
