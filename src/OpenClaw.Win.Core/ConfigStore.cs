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
        var config = JsonSerializer.Deserialize<AppConfig>(json, SerializerOptions);
        return config ?? new AppConfig();
    }

    public void Save(AppConfig config)
    {
        AppPaths.EnsureDirectories();
        var json = JsonSerializer.Serialize(config, SerializerOptions);
        File.WriteAllText(AppPaths.ConfigPath, json);
    }
}
