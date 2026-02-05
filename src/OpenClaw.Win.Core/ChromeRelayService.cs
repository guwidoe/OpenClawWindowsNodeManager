using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace OpenClaw.Win.Core;

public class ChromeRelayService
{
    public virtual async Task<bool> VerifyRelayAsync(int port, CancellationToken cancellationToken = default)
    {
        var url = $"http://127.0.0.1:{port}/";
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(3) };

        try
        {
            using var response = await http.GetAsync(url, cancellationToken).ConfigureAwait(false);
            return response.IsSuccessStatusCode || (int)response.StatusCode < 500;
        }
        catch
        {
            return false;
        }
    }
}
