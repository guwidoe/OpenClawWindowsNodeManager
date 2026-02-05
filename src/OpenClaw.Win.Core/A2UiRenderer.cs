using System;
using System.Net;
using System.Text.Json;

namespace OpenClaw.Win.Core;

public static class A2UiRenderer
{
    public static CanvasContent Render(string payload)
    {
        if (string.IsNullOrWhiteSpace(payload))
        {
            return new CanvasContent(BuildHtml("Canvas is idle.", "Waiting for agent output."));
        }

        var trimmed = payload.TrimStart();
        if (trimmed.StartsWith("<", StringComparison.Ordinal))
        {
            return new CanvasContent(payload);
        }

        var pretty = TryFormatJson(payload) ?? payload;
        var escaped = WebUtility.HtmlEncode(pretty);
        var html = BuildHtml("Agent UI", $"<pre>{escaped}</pre>");
        return new CanvasContent(html, "Agent UI", "text/html");
    }

    private static string? TryFormatJson(string payload)
    {
        try
        {
            using var doc = JsonDocument.Parse(payload);
            return JsonSerializer.Serialize(doc, new JsonSerializerOptions { WriteIndented = true });
        }
        catch
        {
            return null;
        }
    }

    private static string BuildHtml(string title, string body)
    {
        return $$"""
                 <!doctype html>
                 <html lang="en">
                 <head>
                   <meta charset="utf-8" />
                   <meta name="viewport" content="width=device-width, initial-scale=1" />
                   <title>{{WebUtility.HtmlEncode(title)}}</title>
                   <style>
                     :root {
                       color-scheme: light dark;
                       font-family: "Segoe UI", "Bahnschrift", system-ui, sans-serif;
                       line-height: 1.4;
                     }
                     body {
                       margin: 0;
                       padding: 16px;
                       background: #0e1118;
                       color: #e6e9f0;
                     }
                     .card {
                       background: #161b26;
                       border: 1px solid #2a3240;
                       border-radius: 12px;
                       padding: 16px;
                     }
                     h1 {
                       margin: 0 0 12px 0;
                       font-size: 18px;
                       font-weight: 600;
                     }
                     pre {
                       margin: 0;
                       white-space: pre-wrap;
                       font-size: 12px;
                       background: #0b0f16;
                       padding: 12px;
                       border-radius: 8px;
                       border: 1px solid #202938;
                     }
                   </style>
                 </head>
                 <body>
                   <div class="card">
                     <h1>{{WebUtility.HtmlEncode(title)}}</h1>
                     {{body}}
                   </div>
                 </body>
                 </html>
                 """;
    }
}
