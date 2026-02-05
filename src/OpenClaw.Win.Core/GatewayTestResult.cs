namespace OpenClaw.Win.Core;

public sealed class GatewayTestResult
{
    public bool DnsResolved { get; set; }
    public bool TcpConnected { get; set; }
    public bool TlsHandshake { get; set; }
    public bool HttpReachable { get; set; }
    public bool WebSocketConnected { get; set; }
    public string? ErrorMessage { get; set; }
}
