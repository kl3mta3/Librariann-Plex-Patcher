using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using LibrariannPlexPatcher.Models;
using LibrariannPlexPatcher.Services;
using Microsoft.Win32;

namespace LibrariannPlexPatcher;

public partial class MainWindow : Window
{
    private readonly PatcherSettings _settings = SettingsStore.Load();

    public MainWindow()
    {
        InitializeComponent();

        HeaderLogoImage.Source = LoadEmbeddedImage("logo-white-64.png");

        PlexFolderTextBox.Text = string.IsNullOrWhiteSpace(_settings.PlexInstallFolder)
            ? GuessDefaultPlexFolder()
            : _settings.PlexInstallFolder;
        LibrariannAddressTextBox.Text = _settings.LibrariannAddress;

        RefreshStatusDisplay();
    }

    private static string GuessDefaultPlexFolder()
    {
        const string common = @"C:\Program Files\Plex\Plex Media Server";
        return Directory.Exists(common) ? common : "";
    }

    private static BitmapImage LoadEmbeddedImage(string fileName)
    {
        var assembly = Assembly.GetExecutingAssembly();
        using var stream = assembly.GetManifestResourceStream($"LibrariannPlexPatcher.Assets.static.{fileName}")
            ?? throw new InvalidOperationException($"Embedded image {fileName} not found.");
        var image = new BitmapImage();
        image.BeginInit();
        image.CacheOption = BitmapCacheOption.OnLoad;
        image.StreamSource = stream;
        image.EndInit();
        image.Freeze();
        return image;
    }

    private void BrowseButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog
        {
            Title = "Select the Plex Media Server install folder",
            InitialDirectory = Directory.Exists(PlexFolderTextBox.Text)
                ? PlexFolderTextBox.Text
                : @"C:\Program Files\Plex",
        };
        if (dialog.ShowDialog() == true)
        {
            PlexFolderTextBox.Text = dialog.FolderName;
        }
    }

    private async void TestConnectionButton_Click(object sender, RoutedEventArgs e)
    {
        var url = ConnectionTester.Normalize(LibrariannAddressTextBox.Text);
        if (string.IsNullOrWhiteSpace(url))
        {
            SetStatus(ConnectionStatusText, "Enter an address first.", isError: true);
            return;
        }

        TestConnectionButton.IsEnabled = false;
        SetStatus(ConnectionStatusText, "Testing...", isError: false, isMuted: true);

        var (success, message) = await ConnectionTester.TestAsync(url);
        SetStatus(ConnectionStatusText, message, isError: !success, isSuccess: success);

        TestConnectionButton.IsEnabled = true;
    }

    private async void PatchButton_Click(object sender, RoutedEventArgs e)
    {
        var plexFolder = PlexFolderTextBox.Text.Trim();
        var librariannUrl = ConnectionTester.Normalize(LibrariannAddressTextBox.Text);

        if (!Directory.Exists(plexFolder))
        {
            SetStatus(ActionStatusText, "That Plex folder doesn't exist.", isError: true);
            return;
        }
        if (string.IsNullOrWhiteSpace(librariannUrl))
        {
            SetStatus(ActionStatusText, "Enter a Librariann address first.", isError: true);
            return;
        }

        PatchButton.IsEnabled = false;
        RestoreButton.IsEnabled = false;
        SetStatus(ActionStatusText, "Locating Plex's web client...", isError: false, isMuted: true);

        var indexPath = await Task.Run(() => PlexIndexLocator.Find(plexFolder));
        if (indexPath == null)
        {
            SetStatus(ActionStatusText, "Couldn't find Plex's bundled web client (index.html) under that folder.", isError: true);
            PatchButton.IsEnabled = true;
            RefreshStatusDisplay();
            return;
        }

        var staticFolder = Path.Combine(Path.GetDirectoryName(indexPath)!, "static");

        _settings.PlexInstallFolder = plexFolder;
        _settings.LibrariannAddress = LibrariannAddressTextBox.Text.Trim();
        _settings.ResolvedIndexHtmlPath = indexPath;
        SettingsStore.Save(_settings);

        SetStatus(ActionStatusText, "Requesting admin permission...", isError: false, isMuted: true);

        var args = new ElevatedActionArgs
        {
            Action = "patch",
            IndexHtmlPath = indexPath,
            StaticFolderPath = staticFolder,
            LibrariannUrl = librariannUrl,
            BackupFolder = string.IsNullOrWhiteSpace(_settings.BackupPath)
                ? SettingsStore.DefaultBackupFolder
                : Path.GetDirectoryName(_settings.BackupPath)!,
            ExistingBackupPath = _settings.BackupPath,
        };

        var result = await ElevatedActionRunner.RunAsync(args);

        if (result.Success)
        {
            if (!string.IsNullOrWhiteSpace(result.BackupPath) && result.BackupPath != _settings.BackupPath)
            {
                _settings.BackupPath = result.BackupPath;
                _settings.LastBackupUtc = DateTime.UtcNow;
            }
            _settings.LastPatchUtc = DateTime.UtcNow;
            SettingsStore.Save(_settings);
            SetStatus(ActionStatusText, "Patched successfully.", isError: false, isSuccess: true);
        }
        else
        {
            SetStatus(ActionStatusText, $"Patch failed: {result.Error}", isError: true);
        }

        RefreshStatusDisplay();
        PatchButton.IsEnabled = true;
    }

    private async void RestoreButton_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_settings.ResolvedIndexHtmlPath) || string.IsNullOrWhiteSpace(_settings.BackupPath))
        {
            SetStatus(ActionStatusText, "No backup to restore from yet.", isError: true);
            return;
        }

        PatchButton.IsEnabled = false;
        RestoreButton.IsEnabled = false;
        SetStatus(ActionStatusText, "Requesting admin permission...", isError: false, isMuted: true);

        var args = new ElevatedActionArgs
        {
            Action = "restore",
            IndexHtmlPath = _settings.ResolvedIndexHtmlPath,
            ExistingBackupPath = _settings.BackupPath,
            StaticFolderPath = "",
            LibrariannUrl = "",
            BackupFolder = "",
        };

        var result = await ElevatedActionRunner.RunAsync(args);

        if (result.Success)
        {
            _settings.LastPatchUtc = null;
            SettingsStore.Save(_settings);
            SetStatus(ActionStatusText, "Restored Plex's original index.html.", isError: false, isSuccess: true);
        }
        else
        {
            SetStatus(ActionStatusText, $"Restore failed: {result.Error}", isError: true);
        }

        RefreshStatusDisplay();
        PatchButton.IsEnabled = true;
    }

    private void RefreshStatusDisplay()
    {
        BackupPathText.Text = string.IsNullOrWhiteSpace(_settings.BackupPath)
            ? "No backup yet."
            : $"Backup: {_settings.BackupPath}";
        LastBackupText.Text = _settings.LastBackupUtc is { } backupUtc
            ? $"Last backed up: {backupUtc.ToLocalTime():g}"
            : " ";
        LastPatchText.Text = _settings.LastPatchUtc is { } patchUtc
            ? $"Last patched: {patchUtc.ToLocalTime():g}"
            : "Not currently patched.";
        RestoreButton.IsEnabled = !string.IsNullOrWhiteSpace(_settings.BackupPath) && File.Exists(_settings.BackupPath);
    }

    private void SetStatus(System.Windows.Controls.TextBlock target, string text, bool isError, bool isSuccess = false, bool isMuted = false)
    {
        target.Text = text;
        target.Foreground = (Brush) FindResource(
            isError ? "ErrorBrush" : isSuccess ? "SuccessBrush" : "MutedTextBrush");
    }
}
