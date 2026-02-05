using System;
using System.Net;
using System.Net.Http;
using System.Net.Security;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

namespace OpenClaw.Win.Core;

public sealed class GatewayTester
{
    public async Task<GatewayTestResult> TestAsync(AppConfig config, CancellationToken cancellationToken = default)
    {
        var result = new GatewayTestResult();

        if (string.IsNullOrWhiteSpace(config.GatewayHost))
        {
            result.ErrorMessage = "Gateway host not set.";
            return result;
        }

        try
        {
            await Dns.GetHostEntryAsync(config.GatewayHost);
            result.DnsResolved = true;
        }
        catch (Exception ex)
        {
            result.ErrorMessage = $"DNS lookup failed: {ex.Message}";
            return result;
        }

        using (var tcp = new TcpClient())
        {
            try
            {
                using var connectCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                connectCts.CancelAfter(TimeSpan.FromSeconds(5));
                await tcp.ConnectAsync(config.GatewayHost, config.GatewayPort, connectCts.Token);
                result.TcpConnected = true;

                if (config.UseTls)
                {
                    using var ssl = new SslStream(
                        tcp.GetStream(),
                        false,
                        (sender, cert, chain, errors) => ValidateCertificate(cert, errors, config.TlsFingerprint));

                    await ssl.AuthenticateAsClientAsync(config.GatewayHost);
                    result.TlsHandshake = true;
                }
                else
                {
                    result.TlsHandshake = true;
                }
            }
            catch (Exception ex)
            {
                result.ErrorMessage = $"TCP/TLS failed: {ex.Message}";
                return result;
            }
        }

        try
        {
            var scheme = config.UseTls ? "https" : "http";
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
            using var response = await http.GetAsync($"{scheme}://{config.GatewayHost}:{config.GatewayPort}/", cancellationToken);
            result.HttpReachable = response.StatusCode != HttpStatusCode.NotFound;
        }
        catch
        {
            result.HttpReachable = false;
        }

        try
        {
            var scheme = config.UseTls ? "wss" : "ws";
            using var ws = new ClientWebSocket();
            using var wsCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            wsCts.CancelAfter(TimeSpan.FromSeconds(5));
            await ws.ConnectAsync(new Uri($"{scheme}://{config.GatewayHost}:{config.GatewayPort}/"), wsCts.Token);
            result.WebSocketConnected = ws.State == WebSocketState.Open;
            ws.Abort();
        }
        catch
        {
            result.WebSocketConnected = false;
        }

        return result;
    }

    private static bool ValidateCertificate(System.Security.Cryptography.X509Certificates.X509Certificate? cert, SslPolicyErrors errors, string? fingerprint)
    {
        if (cert == null)
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(fingerprint))
        {
            var expected = fingerprint.Replace(":", string.Empty).ToLowerInvariant();
            var actual = cert.GetCertHashString(HashAlgorithmName.SHA256).ToLowerInvariant();
            return string.Equals(expected, actual, StringComparison.OrdinalIgnoreCase);
        }

        return errors == SslPolicyErrors.None;
    }
}
