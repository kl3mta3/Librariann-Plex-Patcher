namespace LibrariannPlexPatcher.Models;

/// <summary>
/// Persisted to %AppData%\LibrariannPlexPatcher\settings.json. Everything here is safe to read/write
/// without elevation - none of it is the actual patch/restore file operation.
/// </summary>
public sealed class PatcherSettings
{
    public string PlexInstallFolder { get; set; } = "";
    public string LibrariannAddress { get; set; } = "";

    /// <summary>Cached from the last successful search, so re-patching doesn't need to re-search the
    /// whole Plex folder tree every time - cleared implicitly by just re-running Patch if it's wrong
    /// (a fresh search always happens on click; this is only used to know what to restore against).</summary>
    public string? ResolvedIndexHtmlPath { get; set; }

    /// <summary>Path to the one true backup of Plex's original, pre-patch index.html. Never
    /// overwritten by a later patch once set - see ElevatedActionExecutor.Patch.</summary>
    public string? BackupPath { get; set; }

    public DateTime? LastBackupUtc { get; set; }
    public DateTime? LastPatchUtc { get; set; }
}
