using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Windows;
using MailScanner.App.Models;
using MailScanner.App.Services;
using MailScanner.Core.Services;
using MailScanner.Core.Models;

namespace MailScanner.App;

public partial class MainWindow : Window, INotifyPropertyChanged
{
    private readonly IAppSettingsProvider settingsProvider;
    private readonly IMailImportService mailImportService;
    private readonly IMailConnectionTestService mailConnectionTestService;
    private readonly IDocumentCandidateStore documentCandidateStore;
    private readonly IDocumentDownloadService documentDownloadService;
    private readonly AppVersionService appVersionService;
    private readonly GitHubReleaseUpdateService releaseUpdateService;
    private string accountCountSummary = "0 Konten";
    private string busyMessage = string.Empty;
    private Visibility busyVisibility = Visibility.Collapsed;
    private string currentVersionSummary = string.Empty;
    private string currentScanTarget = string.Empty;
    private string excludedFoldersSummary = "Ausgeschlossene Ordner: keine";
    private string invoiceMatchSummary = "0 Rechnungs-Treffer";
    private string lastConnectionTestSummary = string.Empty;
    private GitHubReleaseUpdateService.ReleaseUpdateInfo latestReleaseInfo = GitHubReleaseUpdateService.ReleaseUpdateInfo.Unavailable();
    private string latestReleaseUrl = string.Empty;
    private Visibility latestReleaseVisibility = Visibility.Collapsed;
    private string attachmentMailSummary = "0 Mails mit Anhang";
    private string lookbackScopeSummary = "Scanbereich: kompletter Verlauf";
    private string oldestMailSummary = "Aelteste gescannte Mail: noch kein Scan";
    private string pdfCandidateSummary = "0 PDF-Kandidaten";
    private string scanProgressSummary = string.Empty;
    private string searchText = string.Empty;
    private string statusMessage = "Bereit. Bitte zuerst die Konten verwalten oder einen Verbindungstest starten.";
    private string updateStatusSummary = "Pruefe GitHub-Releases nach dem Start...";

    public MainWindow(
        IAppSettingsProvider settingsProvider,
        IMailImportService mailImportService,
        IMailConnectionTestService mailConnectionTestService,
        IDocumentCandidateStore documentCandidateStore,
        IDocumentDownloadService documentDownloadService,
        AppVersionService appVersionService,
        GitHubReleaseUpdateService releaseUpdateService)
    {
        this.settingsProvider = settingsProvider;
        this.mailImportService = mailImportService;
        this.mailConnectionTestService = mailConnectionTestService;
        this.documentCandidateStore = documentCandidateStore;
        this.documentDownloadService = documentDownloadService;
        this.appVersionService = appVersionService;
        this.releaseUpdateService = releaseUpdateService;

        InitializeComponent();
        DataContext = this;
        Loaded += OnLoaded;
        CurrentVersionSummary = $"v{appVersionService.GetCurrentVersion()}";
        RefreshExcludedFolderSummary();
        RefreshLookbackSummary();
        RefreshAccountSummary();
    }

    public ObservableCollection<CandidateListItem> Candidates { get; } = [];

    public string AccountCountSummary
    {
        get => accountCountSummary;
        set
        {
            accountCountSummary = value;
            OnPropertyChanged();
        }
    }

    public string BusyMessage
    {
        get => busyMessage;
        set
        {
            busyMessage = value;
            OnPropertyChanged();
        }
    }

    public string AttachmentMailSummary
    {
        get => attachmentMailSummary;
        set
        {
            attachmentMailSummary = value;
            OnPropertyChanged();
        }
    }

    public Visibility BusyVisibility
    {
        get => busyVisibility;
        set
        {
            busyVisibility = value;
            OnPropertyChanged();
        }
    }

    public string ScanProgressSummary
    {
        get => scanProgressSummary;
        set
        {
            scanProgressSummary = value;
            OnPropertyChanged();
        }
    }

    public string CurrentScanTarget
    {
        get => currentScanTarget;
        set
        {
            currentScanTarget = value;
            OnPropertyChanged();
        }
    }

    public string CurrentVersionSummary
    {
        get => currentVersionSummary;
        set
        {
            currentVersionSummary = value;
            OnPropertyChanged();
        }
    }

    public string PdfCandidateSummary
    {
        get => pdfCandidateSummary;
        set
        {
            pdfCandidateSummary = value;
            OnPropertyChanged();
        }
    }

    public string InvoiceMatchSummary
    {
        get => invoiceMatchSummary;
        set
        {
            invoiceMatchSummary = value;
            OnPropertyChanged();
        }
    }

    public string OldestMailSummary
    {
        get => oldestMailSummary;
        set
        {
            oldestMailSummary = value;
            OnPropertyChanged();
        }
    }

    public string UpdateStatusSummary
    {
        get => updateStatusSummary;
        set
        {
            updateStatusSummary = value;
            OnPropertyChanged();
        }
    }

    public Visibility LatestReleaseVisibility
    {
        get => latestReleaseVisibility;
        set
        {
            latestReleaseVisibility = value;
            OnPropertyChanged();
        }
    }

    public string LookbackScopeSummary
    {
        get => lookbackScopeSummary;
        set
        {
            lookbackScopeSummary = value;
            OnPropertyChanged();
        }
    }

    public string ExcludedFoldersSummary
    {
        get => excludedFoldersSummary;
        set
        {
            excludedFoldersSummary = value;
            OnPropertyChanged();
        }
    }

    public string StatusMessage
    {
        get => statusMessage;
        set
        {
            statusMessage = value;
            OnPropertyChanged();
        }
    }

    public string SearchText
    {
        get => searchText;
        set
        {
            if (searchText == value)
            {
                return;
            }

            searchText = value;
            OnPropertyChanged();
            _ = ApplySearchAsync();
        }
    }

    public string LastConnectionTestSummary
    {
        get => lastConnectionTestSummary;
        set
        {
            lastConnectionTestSummary = value;
            OnPropertyChanged();
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        var cachedCandidates = await documentCandidateStore.SearchAsync(SearchText);
        ReplaceCandidates(cachedCandidates);
        StatusMessage = Candidates.Count == 0
            ? "Bereit. Konten koennen jetzt verwaltet und getestet werden."
            : $"{Candidates.Count} lokale Dokumentkandidaten geladen.";

        _ = CheckForUpdatesAsync();
    }

    private void OnOpenSettingsClicked(object sender, RoutedEventArgs e)
    {
        var window = new AccountSettingsWindow(settingsProvider, mailConnectionTestService)
        {
            Owner = this
        };

        window.ShowDialog();
        RefreshExcludedFolderSummary();
        RefreshLookbackSummary();
        RefreshAccountSummary();
        StatusMessage = "Konten-Setup aktualisiert.";
    }

    private async void OnRefreshClicked(object sender, RoutedEventArgs e)
    {
        await RefreshAsync();
    }

    private async void OnTestConnectionsClicked(object sender, RoutedEventArgs e)
    {
        SetBusyState(true, "Pruefe die Verbindung zu allen konfigurierten Postfaechern...");

        try
        {
            StatusMessage = "Teste IMAP-Verbindungen...";
            var results = await mailConnectionTestService.TestConnectionsAsync();
            var successCount = results.Count(x => x.Success);
            var failed = results.Where(x => !x.Success).ToArray();

            LastConnectionTestSummary = string.Join(" | ", results.Select(x =>
                x.Success
                    ? $"{x.DisplayName}: OK"
                    : $"{x.DisplayName}: FEHLER - {x.Message}"));

            StatusMessage = failed.Length == 0
                ? $"Alle {successCount} Konten erfolgreich getestet."
                : $"{successCount} Konten ok, {failed.Length} mit Fehler.";
        }
        catch (Exception ex)
        {
            LastConnectionTestSummary = string.Empty;
            StatusMessage = $"Verbindungstest fehlgeschlagen: {ex.Message}";
        }
        finally
        {
            SetBusyState(false);
        }
    }

    private async void OnDownloadSelectionClicked(object sender, RoutedEventArgs e)
    {
        var selectedCandidates = CandidatesGrid.SelectedItems.Cast<CandidateListItem>().Select(item => item.Candidate).ToArray();

        if (selectedCandidates.Length == 0)
        {
            StatusMessage = "Bitte zuerst eine oder mehrere Zeilen auswaehlen.";
            return;
        }

        SetBusyState(true, $"Speichere {selectedCandidates.Length} ausgewaehlte PDF-Anhaenge...");

        try
        {
            StatusMessage = $"Lade {selectedCandidates.Length} PDF-Anhaenge herunter...";
            var result = await documentDownloadService.DownloadAsync(selectedCandidates);
            var currentCandidates = await documentCandidateStore.SearchAsync(SearchText);
            ReplaceCandidates(currentCandidates);

            StatusMessage = result.Errors.Count == 0
                ? $"{result.DownloadedDocuments.Count} Dokumente gespeichert."
                : $"{result.DownloadedDocuments.Count} gespeichert, {result.Errors.Count} Fehler.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Download fehlgeschlagen: {ex.Message}";
        }
        finally
        {
            SetBusyState(false);
        }
    }

    private async Task RefreshAsync()
    {
        SetBusyState(true, "Suche nach neuen E-Mails mit PDF-Anhaengen. Das kann je nach Postfach kurz dauern...");
        var progress = new Progress<MailImportProgress>(UpdateImportProgress);

        try
        {
            StatusMessage = "Pruefe IMAP-Konten und gleiche mit SQLite ab...";
            var candidates = await mailImportService.ImportNewCandidatesAsync(progress);
            ReplaceCandidates(FilterCandidates(candidates));
            StatusMessage = Candidates.Count == 0
                ? "Scan abgeschlossen. Es wurden keine neuen PDF-Anhaenge gefunden."
                : $"Scan abgeschlossen. {Candidates.Count} Dokumentkandidaten in der lokalen Ansicht.";
        }
        catch (Exception ex)
        {
            var cachedCandidates = await documentCandidateStore.SearchAsync(SearchText);
            ReplaceCandidates(cachedCandidates);
            StatusMessage = $"Import fehlgeschlagen: {ex.Message}";
        }
        finally
        {
            SetBusyState(false);
        }
    }

    private async Task ApplySearchAsync()
    {
        var filteredCandidates = await documentCandidateStore.SearchAsync(SearchText);
        ReplaceCandidates(filteredCandidates);

        StatusMessage = string.IsNullOrWhiteSpace(SearchText)
            ? $"{Candidates.Count} Dokumentkandidaten in der lokalen Ansicht."
            : $"{Candidates.Count} Treffer fuer '{SearchText.Trim()}'.";
    }

    private IEnumerable<DocumentCandidate> FilterCandidates(IEnumerable<DocumentCandidate> candidates)
    {
        if (string.IsNullOrWhiteSpace(SearchText))
        {
            return candidates;
        }

        var term = SearchText.Trim();

        return candidates.Where(candidate =>
            candidate.AccountName.Contains(term, StringComparison.OrdinalIgnoreCase)
            || candidate.AccountAddress.Contains(term, StringComparison.OrdinalIgnoreCase)
            || candidate.Sender.Contains(term, StringComparison.OrdinalIgnoreCase)
            || candidate.Subject.Contains(term, StringComparison.OrdinalIgnoreCase)
            || candidate.AttachmentName.Contains(term, StringComparison.OrdinalIgnoreCase)
            || candidate.StoredFilePath.Contains(term, StringComparison.OrdinalIgnoreCase)
            || candidate.MessageId.Contains(term, StringComparison.OrdinalIgnoreCase));
    }

    private void RefreshAccountSummary()
    {
        var count = settingsProvider.GetCurrentSettings().MailImport.Accounts.Count;
        AccountCountSummary = count == 1 ? "1 IMAP-Konto" : $"{count} IMAP-Konten";
    }

    private void RefreshLookbackSummary()
    {
        var lookbackDays = settingsProvider.GetCurrentSettings().MailImport.InitialLookbackDays;
        LookbackScopeSummary = lookbackDays <= 0
            ? "Scanbereich: kompletter Verlauf aller Mails"
            : $"Scanbereich: letzte {lookbackDays} Tage";
    }

    private void RefreshExcludedFolderSummary()
    {
        var excludedFolders = settingsProvider.GetCurrentSettings().MailImport.ExcludedFolderPatterns
            .Where(folder => !string.IsNullOrWhiteSpace(folder))
            .Select(folder => folder.Trim())
            .ToArray();

        ExcludedFoldersSummary = excludedFolders.Length == 0
            ? "Ausgeschlossene Ordner: keine"
            : $"Ausgeschlossene Ordner: {string.Join(", ", excludedFolders)}";
    }

    private void ReplaceCandidates(IEnumerable<DocumentCandidate> candidates)
    {
        Candidates.Clear();

        foreach (var candidate in candidates
                     .Select(CandidateListItem.FromCandidate)
                     .OrderByDescending(item => item.PriorityScore)
                     .ThenByDescending(item => item.Candidate.ReceivedAt)
                     .ThenBy(item => item.AttachmentName))
        {
            Candidates.Add(candidate);
        }
    }

    private void SetBusyState(bool isBusy, string? message = null)
    {
        BusyVisibility = isBusy ? Visibility.Visible : Visibility.Collapsed;
        BusyMessage = isBusy ? message ?? "Bitte warten..." : string.Empty;
        ScanProgressSummary = isBusy ? "Starte Scan..." : string.Empty;
        CurrentScanTarget = string.Empty;

        SettingsButton.IsEnabled = !isBusy;
        TestConnectionButton.IsEnabled = !isBusy;
        RefreshButton.IsEnabled = !isBusy;
        DownloadButton.IsEnabled = !isBusy;
        SearchBox.IsEnabled = !isBusy;
        CandidatesGrid.IsEnabled = !isBusy;
    }

    private void UpdateImportProgress(MailImportProgress progress)
    {
        var lookbackText = progress.IsFullScan
            ? progress.OldestScannedMessageAgeDays is int days
                ? $"Scanbereich: kompletter Verlauf, bisher ca. {days} Tage zurueck"
                : "Scanbereich: kompletter Verlauf"
            : $"Scanbereich: letzte {progress.ConfiguredLookbackDays} Tage";

        LookbackScopeSummary = lookbackText;
        ExcludedFoldersSummary = progress.ExcludedFolderCount == 0
            ? "Ausgeschlossene Ordner: keine"
            : $"Ausgeschlossene Ordner aktiv: {progress.ExcludedFolderCount}";
        OldestMailSummary = progress.OldestScannedMessageDate is DateTimeOffset oldestDate
            ? $"Aelteste gescannte Mail: {oldestDate:dd.MM.yyyy} ({Math.Max(0, progress.OldestScannedMessageAgeDays ?? 0)} Tage)"
            : "Aelteste gescannte Mail: noch kein Treffer im laufenden Scan";
        AttachmentMailSummary = $"{progress.AttachmentMessagesFound} Mails mit Anhang";
        PdfCandidateSummary = $"{progress.PdfCandidatesFound} PDF-Kandidaten";
        InvoiceMatchSummary = $"{progress.InvoiceMatchesFound} Rechnungs-Treffer";
        ScanProgressSummary = $"Konten: {progress.AccountsCompleted + 1}/{progress.AccountsTotal} | Ordner: {Math.Min(progress.FoldersCompleted + 1, progress.FoldersTotal)}/{progress.FoldersTotal} | Mails durchsucht: {progress.MessagesScanned} | Mit Anhang: {progress.AttachmentMessagesFound} | PDFs: {progress.PdfCandidatesFound} | Rechnungen: {progress.InvoiceMatchesFound}";
        CurrentScanTarget = $"Aktuell: {progress.AccountName} -> {progress.FolderName} | {progress.StatusText}";
    }

    private async Task CheckForUpdatesAsync()
    {
        try
        {
            var release = await releaseUpdateService.GetLatestReleaseAsync(appVersionService.GetCurrentVersion());
            latestReleaseInfo = release;

            if (release.IsUpdateAvailable && !string.IsNullOrWhiteSpace(release.ReleaseUrl))
            {
                latestReleaseUrl = release.ReleaseUrl;
                LatestReleaseVisibility = Visibility.Visible;
                UpdateStatusSummary = release.InstallerAsset is null
                    ? $"Neue GitHub-Release verfuegbar: {release.LatestVersion}"
                    : $"Neue Version {release.LatestVersion} inkl. Installer verfuegbar";

                await Dispatcher.InvokeAsync(() => ShowUpdateDialog(release));
                return;
            }

            LatestReleaseVisibility = Visibility.Collapsed;
            UpdateStatusSummary = "GitHub-Release aktuell.";
        }
        catch (Exception ex)
        {
            LatestReleaseVisibility = Visibility.Collapsed;
            UpdateStatusSummary = $"Release-Pruefung derzeit nicht verfuegbar: {ex.Message}";
        }
    }

    private void OnOpenLatestReleaseClicked(object sender, RoutedEventArgs e)
    {
        if (latestReleaseInfo.IsUpdateAvailable)
        {
            ShowUpdateDialog(latestReleaseInfo);
            return;
        }

        if (string.IsNullOrWhiteSpace(latestReleaseUrl))
        {
            return;
        }

        Process.Start(new ProcessStartInfo
        {
            FileName = latestReleaseUrl,
            UseShellExecute = true
        });
    }

    private void ShowUpdateDialog(GitHubReleaseUpdateService.ReleaseUpdateInfo release)
    {
        var window = new UpdateReleaseWindow(releaseUpdateService, release, appVersionService.GetCurrentVersion())
        {
            Owner = this
        };

        window.ShowDialog();
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
