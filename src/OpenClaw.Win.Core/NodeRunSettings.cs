namespace OpenClaw.Win.Core;

public sealed record NodeRunSettings(
    string Host,
    int Port,
    bool UseTls,
    string? TlsFingerprint,
    string DisplayName);
