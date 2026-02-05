using System;
using System.IO;

namespace OpenClaw.Win.Core;

public static class AppPaths
{
    private const string CompanyDir = "OpenClaw";
    private const string AppDir = "WindowsCompanion";

    public static string BaseDir => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        CompanyDir,
        AppDir);

    public static string ConfigPath => Path.Combine(BaseDir, "config.json");
    public static string TokenPath => Path.Combine(BaseDir, "token.dat");
    public static string LogsDir => Path.Combine(BaseDir, "logs");
    public static string AppLogPath => Path.Combine(LogsDir, "app.log");
    public static string NodeLogPath => Path.Combine(LogsDir, "node.log");
    public static string DiagnosticsDir => Path.Combine(BaseDir, "diagnostics");

    public static void EnsureDirectories()
    {
        Directory.CreateDirectory(BaseDir);
        Directory.CreateDirectory(LogsDir);
        Directory.CreateDirectory(DiagnosticsDir);
    }
}
