using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace OpenClaw.Win.Core;

public sealed class ExecApprovalHistoryStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    private readonly string _path;

    public ExecApprovalHistoryStore(string? path = null)
    {
        _path = path ?? AppPaths.ExecApprovalLogPath;
    }

    public string LogPath => _path;

    public void Append(ExecApprovalHistoryEntry entry)
    {
        AppPaths.EnsureDirectories();
        var directory = System.IO.Path.GetDirectoryName(_path);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }
        var json = JsonSerializer.Serialize(entry, SerializerOptions);
        File.AppendAllText(_path, json + Environment.NewLine);
    }

    public IReadOnlyList<ExecApprovalHistoryEntry> ReadRecent(int maxEntries)
    {
        if (maxEntries <= 0)
        {
            return Array.Empty<ExecApprovalHistoryEntry>();
        }

        if (!File.Exists(_path))
        {
            return Array.Empty<ExecApprovalHistoryEntry>();
        }

        var lines = File.ReadAllLines(_path);
        var recent = lines.Skip(Math.Max(0, lines.Length - maxEntries));
        var entries = new List<ExecApprovalHistoryEntry>();

        foreach (var line in recent)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            try
            {
                var entry = JsonSerializer.Deserialize<ExecApprovalHistoryEntry>(line, SerializerOptions);
                if (entry != null)
                {
                    entries.Add(entry);
                }
            }
            catch
            {
                // Ignore malformed lines to keep history resilient.
            }
        }

        return entries;
    }
}
