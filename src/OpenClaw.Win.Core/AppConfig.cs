using System;

namespace OpenClaw.Win.Core;

public sealed class AppConfig
{
    public string GatewayHost { get; set; } = "";
    public int GatewayPort { get; set; } = 443;
    public bool UseTls { get; set; } = true;
    public string? TlsFingerprint { get; set; }
    public string DisplayName { get; set; } = Environment.MachineName;
    public string ControlUiUrl { get; set; } = "";
    public int RelayPort { get; set; } = 18792;
    public int PollIntervalSeconds { get; set; } = 5;
    public bool AutoStartTray { get; set; } = true;
    public string? SshHost { get; set; }
    public string? SshUser { get; set; }
    public int SshPort { get; set; } = 22;
    public string? SshCommand { get; set; }
}
