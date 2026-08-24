using System.Net;
using System.Net.Http;

namespace LibrariannPlexPatcher.Services;

public static class ConnectionTester
{
    public static async Task<(bool Success, string Message)> TestAsync(string librariannUrl)
    {
        try
        {
            using var client = new HttpClient {Timeout = TimeSpan.FromSeconds(6)};
            using var response = await client.GetAsync(librariannUrl.TrimEnd('/') + "/embed");
            return response.IsSuccessStatusCode
                ? (true, $"Connected ({(int) response.StatusCode} {response.StatusCode})")
                : (false, $"Unexpected response: {(int) response.StatusCode} {response.StatusCode}");
        }
        catch (Exception ex)
        {
            return (false, $"Unreachable: {ex.Message}");
        }
    }

    /// <summary>
    /// Normalizes a user-entered address (bare domain, ip:port, or a full URL) into a usable base URL.
    /// Defaults to http:// for anything that looks like a local/LAN address (localhost, an IP literal)
    /// and https:// for an actual domain name - matching the common split between a same-machine/LAN
    /// Librariann and one reached through a TLS-terminating tunnel (e.g. Cloudflare Tunnel).
    /// </summary>
    public static string Normalize(string input)
    {
        input = input.Trim().TrimEnd('/');
        if (input.Length == 0) return input;
        if (input.Contains("://", StringComparison.Ordinal)) return input;

        var hostPart = input.Split('/')[0].Split(':')[0];
        var looksLocal = hostPart is "localhost" || IPAddress.TryParse(hostPart, out _);
        return (looksLocal ? "http://" : "https://") + input;
    }
}
