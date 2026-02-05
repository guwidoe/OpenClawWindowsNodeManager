using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace OpenClaw.Win.Core;

public sealed class ConfigStore : IConfigStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    public bool Exists => File.Exists(AppPaths.ConfigPath);

    public AppConfig Load()
    {
        AppPaths.EnsureDirectories();

        if (!File.Exists(AppPaths.ConfigPath))
        {
            return new AppConfig();
        }

        var json = File.ReadAllText(AppPaths.ConfigPath);
        var config = JsonSerializer.Deserialize<AppConfig>(json, SerializerOptions) ?? new AppConfig();
        ApplyLegacyThemePreference(config, json);
        return config;
    }

    public void Save(AppConfig config)
    {
        AppPaths.EnsureDirectories();
        var json = JsonSerializer.Serialize(config, SerializerOptions);
        File.WriteAllText(AppPaths.ConfigPath, json);
    }

    private static void ApplyLegacyThemePreference(AppConfig config, string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("themePreference", out _))
            {
                return;
            }

            if (!doc.RootElement.TryGetProperty("useDarkTheme", out var legacy))
            {
                return;
            }

            var useDark = legacy.ValueKind switch
            {
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                JsonValueKind.String when bool.TryParse(legacy.GetString(), out var parsed) => parsed,
                _ => (bool?)null
            };

            if (useDark.HasValue)
            {
                config.ThemePreference = useDark.Value ? ThemePreference.Dark : ThemePreference.Light;
            }
        }
        catch
        {
            // Best-effort migration only.
        }
    }
}
