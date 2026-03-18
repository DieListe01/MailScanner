using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using MailScanner.App.Services;

namespace MailScanner.App;

public partial class UpdateReleaseWindow : Window, INotifyPropertyChanged
{
    private readonly GitHubReleaseUpdateService releaseUpdateService;
    private readonly GitHubReleaseUpdateService.ReleaseUpdateInfo releaseInfo;
    private string statusMessage;

    public UpdateReleaseWindow(GitHubReleaseUpdateService releaseUpdateService, GitHubReleaseUpdateService.ReleaseUpdateInfo releaseInfo, string currentVersion)
    {
        this.releaseUpdateService = releaseUpdateService;
        this.releaseInfo = releaseInfo;
        statusMessage = releaseInfo.InstallerAsset is null
            ? "Kein Installer-Asset in der Release gefunden."
            : "Der Installer wird heruntergeladen und danach direkt gestartet.";

        InitializeComponent();
        DataContext = this;

        VersionSummary = $"Installiert: {currentVersion}   Neu: {releaseInfo.LatestVersion}";
        ReleaseTitle = releaseInfo.ReleaseTitle;
        ReleaseNotes = string.IsNullOrWhiteSpace(releaseInfo.ReleaseNotes)
            ? "Keine Release-Notizen vorhanden."
            : releaseInfo.ReleaseNotes;
        InstallerSummary = releaseInfo.InstallerAsset is null
            ? "Zur freigegebenen Version gibt es noch keinen Installer-Download."
            : $"Installer gefunden: {releaseInfo.InstallerAsset.FileName}";
    }

    public string VersionSummary { get; }
    public string ReleaseTitle { get; }
    public string ReleaseNotes { get; }
    public string InstallerSummary { get; }

    public string StatusMessage
    {
        get => statusMessage;
        set
        {
            statusMessage = value;
            OnPropertyChanged();
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnOpenReleasePageClicked(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(releaseInfo.ReleaseUrl))
        {
            return;
        }

        Process.Start(new ProcessStartInfo
        {
            FileName = releaseInfo.ReleaseUrl,
            UseShellExecute = true
        });
    }

    private async void OnInstallUpdateClicked(object sender, RoutedEventArgs e)
    {
        if (releaseInfo.InstallerAsset is null)
        {
            StatusMessage = "Kein Installer-Asset verfuegbar. Bitte die Release-Seite nutzen.";
            return;
        }

        try
        {
            StatusMessage = "Lade Installer herunter...";
            var targetDirectory = Path.Combine(Path.GetTempPath(), "MailScanner", "updates", releaseInfo.LatestVersion.TrimStart('v', 'V'));
            
            // Create progress handler for download progress
            IProgress<double> progress = new Progress<double>(percent =>
            {
                // Update UI on dispatcher thread
                Dispatcher.Invoke(() =>
                {
                    StatusMessage = $"Lade Installer herunter... {percent:F1}%";
                });
            });
            
            var installerPath = await releaseUpdateService.DownloadInstallerAsync(
                releaseInfo.InstallerAsset, 
                targetDirectory, 
                progress);

            StatusMessage = "Download abgeschlossen. Bereite Update vor...";
            
            // Close the main application so the installer can replace files
            System.Windows.Application.Current.MainWindow?.Close();
            
            // Wait a moment to ensure the application is fully closed
            await Task.Delay(1000);

            StatusMessage = "Starte Update-Installer....";
            
            // Start the installer - it will handle updating the application and restarting it
            Process.Start(new ProcessStartInfo
            {
                FileName = installerPath,
                UseShellExecute = true
            });

            // Close this update window
            DialogResult = true;
            Close();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Update fehlgeschlagen: {ex.Message} Bitte alternativ die Release-Seite oeffnen.";
        }
    }

    private void OnCloseClicked(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}