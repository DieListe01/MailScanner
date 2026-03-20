using System.Diagnostics;
using System.IO;
using System.Windows;
using MailScanner.App.Services;

namespace MailScanner.App;

public partial class MainWindow
{
    private static readonly string DefaultInstallDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
        "MailScanner");

    private string updatePanelVersionSummary = "Installiert: --";
    private string updatePanelReleaseTitle = "GitHub Release";
    private string updatePanelReleaseNotes = "Release-Informationen werden geladen...";
    private string updatePanelInstallerSummary = "Es wird nach einem Installer-Asset gesucht.";
    private string updatePanelStatusMessage = "Release-Status wird geprueft...";
    private string updateDownloadProgressSummary = "";
    private double updateDownloadProgressValue;
    private bool isUpdateDownloadInProgress;
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

    public string UpdateDownloadProgressSummary
    {
        get => updateDownloadProgressSummary;
        set { updateDownloadProgressSummary = value; OnPropertyChanged(); NotifyUpdateDownloadStateChanged(); }
    }

    public double UpdateDownloadProgressValue
    {
        get => updateDownloadProgressValue;
        set { updateDownloadProgressValue = value; OnPropertyChanged(); }
    }

    public bool IsUpdateDownloadInProgress
    {
        get => isUpdateDownloadInProgress;
        set { isUpdateDownloadInProgress = value; OnPropertyChanged(); NotifyUpdateDownloadStateChanged(); }
    }

    public Visibility UpdateDownloadProgressVisibility => IsUpdateDownloadInProgress || !string.IsNullOrWhiteSpace(UpdateDownloadProgressSummary)
        ? Visibility.Visible
        : Visibility.Collapsed;

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
        UpdatePanelStatusMessage = $"Release-Status wird geprueft... Standard-Ziel: {DefaultInstallDirectory}";
        UpdateDownloadProgressSummary = "";
        UpdateDownloadProgressValue = 0;
        IsUpdateDownloadInProgress = false;
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
            : $"Installer gefunden: {release.InstallerAsset.FileName} - Standard-Ziel: {DefaultInstallDirectory}";
        UpdatePanelStatusMessage = release.IsUpdateAvailable
            ? $"Neue Version gefunden. Der Installer fuehrt dich anschliessend per 'Weiter' durch die Installation. Standard-Ziel: {DefaultInstallDirectory}"
            : string.IsNullOrWhiteSpace(release.ReleaseUrl)
                ? "Release-Check nicht verfuegbar."
                : "MailScanner ist auf dem neuesten Stand.";
        CanInstallUpdate = release.InstallerAsset is not null;
    }

    private void NotifyUpdateDownloadStateChanged()
    {
        OnPropertyChanged(nameof(UpdateDownloadProgressVisibility));
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
        await StartUpdateDownloadAsync();
    }

    private async void OnQuickUpdateDownloadClicked(object sender, RoutedEventArgs e)
    {
        await StartUpdateDownloadAsync();
    }

    private async Task StartUpdateDownloadAsync()
    {
        if (latestReleaseInfo.InstallerAsset is null)
        {
            UpdatePanelStatusMessage = "Kein Installer-Asset verfuegbar. Bitte die Release-Seite nutzen.";
            UpdateStatusSummary = UpdatePanelStatusMessage;
            return;
        }

        if (IsUpdateDownloadInProgress)
        {
            return;
        }

        try
        {
            IsUpdateDownloadInProgress = true;
            CanInstallUpdate = false;
            UpdateDownloadProgressValue = 0;
            UpdateDownloadProgressSummary = "Download wird vorbereitet...";
            UpdatePanelStatusMessage = "Update wird vorbereitet...";
            UpdateStatusSummary = "Update wird vorbereitet...";

            var targetDirectory = Path.Combine(Path.GetTempPath(), "MailScanner", "updates", latestReleaseInfo.LatestVersion.TrimStart('v', 'V'));
            IProgress<double> progress = new Progress<double>(percent =>
            {
                Dispatcher.Invoke(() =>
                {
                    UpdateDownloadProgressValue = percent;
                    UpdateDownloadProgressSummary = $"Installer wird heruntergeladen... {percent:F1}%";
                    UpdatePanelStatusMessage = UpdateDownloadProgressSummary;
                    UpdateStatusSummary = UpdateDownloadProgressSummary;
                });
            });

            var installerPath = await releaseUpdateService.DownloadInstallerAsync(latestReleaseInfo.InstallerAsset, targetDirectory, progress);
            UpdateDownloadProgressValue = 100;
            UpdateDownloadProgressSummary = "Download abgeschlossen. Installer ist bereit.";
            UpdatePanelStatusMessage = UpdateDownloadProgressSummary;
            UpdateStatusSummary = UpdateDownloadProgressSummary;

            var confirmation = System.Windows.MessageBox.Show(
                $"Der Installer wurde heruntergeladen nach:\n\n{installerPath}\n\nErwartetes Standard-Ziel im Setup:\n{DefaultInstallDirectory}\n\nIm naechsten Schritt sollte das Setup dir den Zielordner anzeigen, bevor du mit 'Weiter' die Installation startest.\n\nInstaller jetzt starten?",
                "Update bereit",
                System.Windows.MessageBoxButton.YesNo,
                System.Windows.MessageBoxImage.Information);

            if (confirmation != System.Windows.MessageBoxResult.Yes)
            {
                UpdatePanelStatusMessage = "Installer heruntergeladen, aber noch nicht gestartet.";
                UpdateStatusSummary = UpdatePanelStatusMessage;
                UpdateDownloadProgressSummary = $"Installer liegt bereit: {installerPath}";
                return;
            }

            UpdatePanelStatusMessage = "Installer wird gestartet...";
            UpdateStatusSummary = UpdatePanelStatusMessage;
            UpdateDownloadProgressSummary = $"Installer wird gestartet. Erwartetes Standard-Ziel: {DefaultInstallDirectory}";

            Process.Start(new ProcessStartInfo
            {
                FileName = installerPath,
                UseShellExecute = true
            });

            UpdateDownloadProgressSummary = "Installer gestartet. MailScanner wird geschlossen...";
            UpdatePanelStatusMessage = UpdateDownloadProgressSummary;
            UpdateStatusSummary = UpdateDownloadProgressSummary;

            Close();
        }
        catch (Exception ex)
        {
            UpdatePanelStatusMessage = $"Update fehlgeschlagen: {ex.Message}";
            UpdateStatusSummary = UpdatePanelStatusMessage;
            UpdateDownloadProgressSummary = UpdatePanelStatusMessage;
            UpdateDownloadProgressValue = 0;
        }
        finally
        {
            IsUpdateDownloadInProgress = false;
            CanInstallUpdate = latestReleaseInfo.InstallerAsset is not null;
        }
    }
}
