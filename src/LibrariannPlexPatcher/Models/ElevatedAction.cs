namespace LibrariannPlexPatcher.Models;

/// <summary>
/// Crosses the process boundary into the elevated relaunch (see ElevatedActionRunner) as a temp JSON
/// file - UseShellExecute (required for the UAC "runas" verb) rules out redirecting stdin/stdout
/// directly, so args and results both go through files instead.
/// </summary>
public sealed class ElevatedActionArgs
{
    /// <summary>"patch" or "restore".</summary>
    public string Action { get; set; } = "";
    public string IndexHtmlPath { get; set; } = "";
    public string StaticFolderPath { get; set; } = "";
    public string LibrariannUrl { get; set; } = "";
    public string BackupFolder { get; set; } = "";
    public string? ExistingBackupPath { get; set; }
    public string ResultFilePath { get; set; } = "";
}

public sealed class ElevatedActionResult
{
    public bool Success { get; set; }
    public string? Error { get; set; }
    public string? BackupPath { get; set; }
    public string? TimestampUtc { get; set; }
}
