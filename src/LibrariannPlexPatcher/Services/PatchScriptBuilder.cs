using System.IO;
using System.Reflection;

namespace LibrariannPlexPatcher.Services;

/// <summary>
/// Builds the injected script block from the embedded template and applies it to Plex's own
/// index.html - either as a fresh insertion (backup-worthy) or a replacement of an existing patch
/// (re-patching after changing settings, no backup needed since one already exists from the real
/// original).
/// </summary>
public static class PatchScriptBuilder
{
    private const string BeginMarker = "<!-- librariann-patch:begin -->";
    private const string EndMarker = "<!-- librariann-patch:end -->";

    // The exact, real anchor confirmed live in Plex's own shipped index.html: an <iframe> tag that is
    // never closed with </iframe>. Under the HTML5 parsing spec, <iframe> is a "raw text" element -
    // once the parser hits this tag it treats everything after it as inert text until a literal
    // </iframe> is found, which never comes here. Anything injected AFTER this tag is silently
    // swallowed as dead text and never becomes a real, executing element - confirmed the hard way
    // during manual testing. The block must go BEFORE this anchor, never after it.
    private const string InsertBeforeAnchor = "<iframe name=\"downloadFileFrame\"";

    public static bool IsAlreadyPatched(string htmlContent) =>
        htmlContent.Contains(BeginMarker, StringComparison.Ordinal);

    public static string BuildBlock(string librariannUrl)
    {
        var template = ReadEmbeddedTemplate();
        var script = template.Replace("__LIBRARIANN_URL__", librariannUrl);
        return BeginMarker + "\n" + script + EndMarker;
    }

    /// <summary>
    /// Returns the new file content. wasFreshInsertion is true when the file had no existing patch
    /// block (the caller should back it up before writing), false when an existing block was replaced
    /// in place (the real original was already backed up by an earlier patch).
    /// </summary>
    public static (string NewContent, bool WasFreshInsertion) Apply(string htmlContent, string librariannUrl)
    {
        var block = BuildBlock(librariannUrl);

        var beginIndex = htmlContent.IndexOf(BeginMarker, StringComparison.Ordinal);
        var endIndex = htmlContent.IndexOf(EndMarker, StringComparison.Ordinal);
        if (beginIndex >= 0 && endIndex > beginIndex)
        {
            var endOfEndMarker = endIndex + EndMarker.Length;
            var replaced = htmlContent[..beginIndex] + block + htmlContent[endOfEndMarker..];
            return (replaced, false);
        }

        var anchorIndex = htmlContent.IndexOf(InsertBeforeAnchor, StringComparison.Ordinal);
        if (anchorIndex < 0)
        {
            // Anchor not found (Plex changed its markup) - fall back to inserting before </body>. Still
            // safe since our own block is a normal, self-closed <script> tag either way; the specific
            // anchor above only matters because of the neighboring unclosed <iframe>'s parsing quirk.
            anchorIndex = htmlContent.LastIndexOf("</body>", StringComparison.OrdinalIgnoreCase);
            if (anchorIndex < 0) anchorIndex = htmlContent.Length;
        }

        var inserted = htmlContent[..anchorIndex] + block + "\n" + htmlContent[anchorIndex..];
        return (inserted, true);
    }

    private static string ReadEmbeddedTemplate()
    {
        var assembly = Assembly.GetExecutingAssembly();
        const string resourceName = "LibrariannPlexPatcher.Assets.patch-script-template.txt";
        using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Embedded resource {resourceName} not found.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
