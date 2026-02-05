using System;

namespace OpenClaw.Win.Core;

public sealed class SshTokenFetchResult
{
    public bool Success { get; init; }
    public string? Token { get; init; }
    public string? ErrorMessage { get; init; }
    public string? Output { get; init; }
}
