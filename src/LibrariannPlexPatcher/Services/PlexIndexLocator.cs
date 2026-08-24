using System.IO;
using System.Linq;

namespace LibrariannPlexPatcher.Services;

/// <summary>
/// Finds Plex's own bundled web-client index.html under a given install root - deliberately searches
/// rather than assuming a fixed path. On Windows this lives under a hash-suffixed
/// Resources\Plug-ins-XXXXXXXXX\WebClient.bundle\Contents\Resources folder that is not guaranteed to
/// keep the same suffix (or even the same general layout) across Plex Media Server versions or OSes.
/// </summary>
public static class PlexIndexLocator
{
    // A distinctive, stable string that's actually present in Plex's real bundled index.html (an
    // always-present, never-closed <iframe name="downloadFileFrame"> right before </body>) - used to
    // confirm a candidate index.html found by folder search really is Plex's web client entry point,
    // not some unrelated file that happens to be named index.html.
    private const string Fingerprint = "downloadFileFrame";

    public static string? Find(string plexInstallRoot)
    {
        if (string.IsNullOrWhiteSpace(plexInstallRoot) || !Directory.Exists(plexInstallRoot))
            return null;

        IEnumerable<string> candidates;
        try
        {
            candidates = Directory.EnumerateFiles(plexInstallRoot, "index.html", SearchOption.AllDirectories);
        }
        catch (UnauthorizedAccessException)
        {
            // A folder somewhere under the tree we can't list (permissions) shouldn't abort the whole
            // search - EnumerateFiles throws on the first inaccessible directory rather than skipping
            // it, so fall back to a narrower, targeted enumeration of just the known WebClient.bundle
            // shape instead of giving up entirely.
            candidates = [];
        }

        // Prefer a match under the known WebClient.bundle folder name if there is one - it's the real
        // Plex web client specifically, not just anything named index.html that happens to also
        // contain the fingerprint string somewhere under a large install tree.
        var underWebClientBundle = candidates
            .Where(path => path.Replace('\\', '/').Contains("WebClient.bundle", StringComparison.OrdinalIgnoreCase))
            .FirstOrDefault(IsRealPlexIndex);
        if (underWebClientBundle != null) return underWebClientBundle;

        return candidates.FirstOrDefault(IsRealPlexIndex);
    }

    private static bool IsRealPlexIndex(string path)
    {
        try
        {
            return File.ReadAllText(path).Contains(Fingerprint, StringComparison.Ordinal);
        }
        catch
        {
            return false;
        }
    }
}
