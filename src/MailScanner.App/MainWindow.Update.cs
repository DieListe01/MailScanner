using System.Diagnostics;
using System.IO;
using System.Windows;
using MailScanner.App.Services;

namespace MailScanner.App;

public partial class MainWindow
{
    private string updatePanelVersionSummary = "Installiert: --";
    private string updatePanelReleaseTitle = "GitHub Release";
    private string updatePanelReleaseNotes = "Release-Informationen werden geladen...";
    private string updatePanelInstallerSummary = "Es wird nach einem Installer-Asset gesucht.";
    private string updatePanelStatusMessage = "Release-Status wird geprueft...";
    private bool canInstallUpdate;

    public string UpdatePanelVersionSummary
    {
        get => updatePanelVersionSummary;
        set { updatePanelVersionSummary = value; OnPropertyChanged(); }
    }

    public string UpdatePanelReleaseTitle
    {
        get => updatePanelReleaseTitle;
        set { updatePanelReleaseTitle = value; OnPropertyChanged(); }
    }

    public string UpdatePanelReleaseNotes
    {
        get => updatePanelReleaseNotes;
        set { updatePanelReleaseNotes = value; OnPropertyChanged(); }
    }

    public string UpdatePanelInstallerSummary
    {
        get => updatePanelInstallerSummary;
        set { updatePanelInstallerSummary = value; OnPropertyChanged(); }
    }

    public string UpdatePanelStatusMessage
    {
        get => updatePanelStatusMessage;
        set { updatePanelStatusMessage = value; OnPropertyChanged(); }
    }

    public bool CanInstallUpdate
    {
        get => canInstallUpdate;
        set { canInstallUpdate = value; OnPropertyChanged(); }
    }

    private void InitializeUpdatePanel()
    {
        UpdatePanelVersionSummary = $"Installiert: {appVersionService.GetCurrentVersion()}";
        UpdatePanelReleaseTitle = "GitHub Release";
        UpdatePanelReleaseNotes = "Release-Notizen werden nach dem Start geladen.";
        UpdatePanelInstallerSummary = "Installer-Asset wird geprueft.";
        UpdatePanelStatusMessage = "Release-Status wird geprueft...";
        CanInstallUpdate = false;
    }

    private void SyncUpdatePanel(GitHubReleaseUpdateService.ReleaseUpdateInfo release)
    {
        var currentVersion = appVersionService.GetCurrentVersion();
        UpdatePanelVersionSummary = string.IsNullOrWhiteSpace(release.LatestVersion)
            ? $"Installiert: {currentVersion}"
            : $"Installiert: {currentVersion}   Neu: {release.LatestVersion}";
        UpdatePanelReleaseTitle = string.IsNullOrWhiteSpace(release.ReleaseTitle) ? "GitHub Release" : release.ReleaseTitle;
        UpdatePanelReleaseNotes = string.IsNullOrWhiteSpace(release.ReleaseNotes)
            ? "Keine Release-Notizen vorhanden."
            : release.ReleaseNotes;
        UpdatePanelInstallerSummary = release.InstallerAsset is null
            ? "Zur aktuell gefundenen Version liegt kein Installer-Asset vor."
            : $"Installer gefunden: {release.InstallerAsset.FileName}";
        UpdatePanelStatusMessage = release.IsUpdateAvailable
            ? "Neue Version gefunden. Du kannst den Installer direkt laden oder die Release-Seite oeffnen."
            : string.IsNullOrWhiteSpace(release.ReleaseUrl)
                ? "Release-Check nicht verfuegbar."
                : "MailScanner ist auf dem neuesten Stand.";
        CanInstallUpdate = release.InstallerAsset is not null;
    }

    private void OnOpenReleasePageClicked(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(latestReleaseInfo.ReleaseUrl))
        {
            return;
        }

        Process.Start(new ProcessStartInfo
        {
            FileName = latestReleaseInfo.ReleaseUrl,
            UseShellExecute = true
        });
    }

    private async void OnInstallUpdateClicked(object sender, RoutedEventArgs e)
    {
        if (latestReleaseInfo.InstallerAsset is null)
        {
            UpdatePanelStatusMessage = "Kein Installer-Asset verfuegbar. Bitte die Release-Seite nutzen.";
            return;
        }

        try
        {
            UpdatePanelStatusMessage = "Lade Installer herunter...";
            var targetDirectory = Path.Combine(Path.GetTempPath(), "MailScanner", "updates", latestReleaseInfo.LatestVersion.TrimStart('v', 'V'));
            IProgress<double> progress = new Progress<double>(percent =>
            {
                Dispatcher.Invoke(() => UpdatePanelStatusMessage = $"Lade Installer herunter... {percent:F1}%");
            });

            var installerPath = await releaseUpdateService.DownloadInstallerAsync(latestReleaseInfo.InstallerAsset, targetDirectory, progress);
            UpdatePanelStatusMessage = "Download abgeschlossen. Installer wird gestartet...";

            Process.Start(new ProcessStartInfo
            {
                FileName = installerPath,
                UseShellExecute = true
            });

            Close();
        }
        catch (Exception ex)
        {
            UpdatePanelStatusMessage = $"Update fehlgeschlagen: {ex.Message}";
        }
    }
}
