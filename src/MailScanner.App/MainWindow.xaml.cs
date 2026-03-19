using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using MailScanner.App.Models;
using MailScanner.App.Services;
using MailScanner.Core.Services;
using MailScanner.Core.Models;
using MailScanner.Core.Enums;

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
    private string liveScanInfo = "Bereit zum Scannen...";
    private string accountSettingsInfo = "Konfigurierte Konten werden geladen...";
    private string excludedFoldersSummary = "Ausgeschlossene Ordner: keine";
    private string invoiceMatchSummary = "0 Rechnungs-Treffer";
    private string lastConnectionTestSummary = string.Empty;
    private GitHubReleaseUpdateService.ReleaseUpdateInfo latestReleaseInfo = GitHubReleaseUpdateService.ReleaseUpdateInfo.Unavailable();
    private string latestReleaseButtonText = "Update";
    private string latestReleaseUrl = string.Empty;
    private string latestVersionSummary = "Pruefung laeuft...";
    private Visibility latestReleaseVisibility = Visibility.Collapsed;
    private string attachmentMailSummary = "0 Mails mit Anhang";
    private string lookbackScopeSummary = "Scanbereich: kompletter Verlauf";
    private string oldestMailSummary = "Aelteste gescannte Mail: noch kein Scan";
    private string pdfCandidateSummary = "0 PDF-Kandidaten";
    private string scanProgressSummary = string.Empty;
    private double scanProgressPercentage = 0;
    private string searchText = string.Empty;
    private string statusMessage = "Bereit. Bitte zuerst die Konten verwalten oder einen Verbindungstest starten.";
    private string selectionInfo = string.Empty;
    private bool onlyWithAttachments = true; // Default: nur mit Anhang
    private bool onlyDocPdf = false;
    private string updateStatusSummary = "Pruefe GitHub-Releases nach dem Start...";
    private ScanLogger scanLogger = new();
    private CancellationTokenSource? cancellationTokenSource;
    private DispatcherTimer? liveUpdateTimer;

    public MainWindow(
        IAppSettingsProvider settingsProvider,
        IMailImportService mailImportService,
        IMailConnectionTestService mailConnectionTestService,
        IDocumentCandidateStore documentCandidateStore,
        IDocumentDownloadService documentDownloadService,
        ScanLogger scanLogger,
        AppVersionService appVersionService,
        GitHubReleaseUpdateService releaseUpdateService)
    {
        this.settingsProvider = settingsProvider;
        this.mailImportService = mailImportService;
        this.mailConnectionTestService = mailConnectionTestService;
        this.documentCandidateStore = documentCandidateStore;
        this.documentDownloadService = documentDownloadService;
        this.scanLogger = scanLogger;
        this.scanLogger.LogChanged += HandleScanLogChanged;
        this.appVersionService = appVersionService;
        this.releaseUpdateService = releaseUpdateService;

        InitializeComponent();
        DataContext = this;
        InitializeUpdatePanel();
        InitializeAccountEditor();
        SetCurrentPage(WorkspacePage.Scanner);
        Loaded += OnLoaded;
        var currentVersion = appVersionService.GetCurrentVersion();
        Title = $"MailScanner v{currentVersion}";
        CurrentVersionSummary = $"Installiert: v{currentVersion}";
        RefreshExcludedFolderSummary();
        RefreshLookbackSummary();
        RefreshAccountSummary();
    }

    private void HandleScanLogChanged()
    {
        Dispatcher.Invoke(() => OnPropertyChanged(nameof(LogText)));
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

    public double ScanProgressPercentage
    {
        get => scanProgressPercentage;
        set
        {
            scanProgressPercentage = value;
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

    public string LiveScanInfo
    {
        get => liveScanInfo;
        set
        {
            liveScanInfo = value;
            OnPropertyChanged();
        }
    }

    public string AccountSettingsInfo
    {
        get => accountSettingsInfo;
        set
        {
            accountSettingsInfo = value;
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

    public string LatestReleaseButtonText
    {
        get => latestReleaseButtonText;
        set
        {
            latestReleaseButtonText = value;
            OnPropertyChanged();
        }
    }

    public string LatestVersionSummary
    {
        get => latestVersionSummary;
        set
        {
            latestVersionSummary = value;
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

    public string SelectionInfo
    {
        get => selectionInfo;
        set
        {
            selectionInfo = value;
            OnPropertyChanged();
        }
    }

    public bool OnlyWithAttachments
    {
        get => onlyWithAttachments;
        set
        {
            onlyWithAttachments = value;
            OnPropertyChanged();
            _ = ApplySearchAsync();
        }
    }

    public bool OnlyDocPdf
    {
        get => onlyDocPdf;
        set
        {
            onlyDocPdf = value;
            OnPropertyChanged();
            _ = ApplySearchAsync();
        }
    }

    public string LogText
    {
        get => scanLogger.GetLogText();
        set
        {
            OnPropertyChanged();
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
        try
        {
            var cachedCandidates = await documentCandidateStore.SearchAsync(SearchText);
            ReplaceCandidates(cachedCandidates);
            StatusMessage = Candidates.Count == 0
                ? "Bereit. Konten koennen jetzt verwaltet und getestet werden."
                : $"{Candidates.Count} lokale Dokumentkandidaten geladen.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Lokale Daten konnten nicht geladen werden: {SimplifyErrorMessage(ex.Message)}";
        }

        UpdateNavigationVisualState();
        _ = CheckForUpdatesAsync();
    }

    private void OnTitleBarMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Left)
        {
            return;
        }

        if (e.ClickCount == 2 && ResizeMode == ResizeMode.CanResize)
        {
            WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
            return;
        }

        DragMove();
    }

    private void OnMinimizeWindowClicked(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    private void OnToggleMaximizeWindowClicked(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
    }

    private void OnCloseWindowClicked(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void OnOpenSettingsClicked(object sender, RoutedEventArgs e)
    {
        LoadAccountEditorSettings();
        SetCurrentPage(WorkspacePage.Accounts);
        StatusMessage = "Kontenansicht geoeffnet.";
    }

    private void OnRefreshClicked(object sender, RoutedEventArgs e)
    {
        _ = RefreshAsync();
    }

    private void OnStopClicked(object sender, RoutedEventArgs e)
    {
        try
        {
            cancellationTokenSource?.Cancel();
            StatusMessage = "Scan wird gestoppt...";
            scanLogger.LogInfo("=== SCAN MANUELL GESTOPPT ===");
            
            // Test logging
            scanLogger.LogInfo($"[STOP] Stop-Button geklickt, BusyVisibility={BusyVisibility}");
        }
        catch (Exception ex)
        {
            StatusMessage = $"Fehler beim Stoppen: {ex.Message}";
            scanLogger.LogError($"Fehler beim Stoppen: {ex.Message}", ex);
        }
    }

    // Test method for debugging - call this from Immediate Window
    public void TestStopButton()
    {
        SetBusyState(true, "Test-Scan läuft...");
        scanLogger.LogInfo("[TEST] Stop-Button sollte jetzt sichtbar sein");
        
        DispatcherTimer timer = new DispatcherTimer 
        { 
            Interval = TimeSpan.FromSeconds(3) 
        };
        timer.Tick += (s, e) => {
            SetBusyState(false);
            timer.Stop();
            scanLogger.LogInfo("[TEST] Stop-Button sollte jetzt unsichtbar sein");
        };
        timer.Start();
    }

    // Test method for live updates
    public void TestLiveUpdates()
    {
        scanLogger.LogInfo("[TEST] Starte Live-Update Test...");
        SetBusyState(true, "Test-Scan läuft...");
        
        int counter = 0;
        DispatcherTimer testTimer = new DispatcherTimer 
        { 
            Interval = TimeSpan.FromMilliseconds(200) // Faster updates for testing
        };
        testTimer.Tick += (s, e) => {
            counter++;
            LiveScanInfo = $"📧 Test-Konto | 📁 Test-Ordner | 📅 Test-Datum | 📎 {counter} Anhänge | 📄 {counter} Treffer";
            PdfCandidateSummary = $"{counter} Dokumente";
            AttachmentMailSummary = $"{counter} Mails mit Anhang";
            
            // Test grid update with fake data - immediate
            var testCandidates = new List<DocumentCandidate>();
            for (int i = 0; i < counter; i++)
            {
                var candidate = new DocumentCandidate
                {
                    Id = Guid.NewGuid(),
                    Subject = $"Test-Mail {i + 1}",
                    Sender = $"test{i}@example.com",
                    AccountName = "Test-Konto",
                    FolderName = "Test-Ordner",
                    AttachmentName = $"dokument_{i + 1}.pdf",
                    ReceivedAt = DateTime.Now.AddDays(-i),
                    Status = DocumentCandidateStatus.Pending
                };
                testCandidates.Add(candidate);
            }
            
            // Force immediate UI update
            ReplaceCandidates(testCandidates);
            scanLogger.LogInfo($"[TEST-GRID] {counter} Test-Einträge zur Liste hinzugefügt (sofort)");
            
            // Force UI refresh
            Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Render, () => {
                scanLogger.LogInfo($"[TEST-RENDER] UI Refresh erzwungen");
            });
            
            if (counter >= 10)
            {
                testTimer.Stop();
                SetBusyState(false);
                scanLogger.LogInfo("[TEST] Live-Update Test beendet");
            }
        };
        testTimer.Start();
    }

    // Test method for live updates with realistic data
    public void TestRealisticLiveUpdates()
    {
        scanLogger.LogInfo("[TEST-REAL] Starte realistischen Live-Update Test...");
        SetBusyState(true, "Realistischer Test-Scan läuft...");
        
        int counter = 0;
        DispatcherTimer testTimer = new DispatcherTimer 
        { 
            Interval = TimeSpan.FromMilliseconds(300) 
        };
        testTimer.Tick += (s, e) => {
            counter++;
            
            // Update live info
            LiveScanInfo = $"📧 gmail@gmail.com | 📁 INBOX | 📅 älteste Mail: vor {counter * 5} Tagen ({DateTime.Now.AddDays(-counter * 5):dd.MM.yyyy}) | 📎 {counter * 2} Anhänge | 📄 {counter} Treffer";
            PdfCandidateSummary = $"{counter} Dokumente";
            AttachmentMailSummary = $"{counter * 2} Mails mit Anhang";
            ScanProgressPercentage = counter * 10.0; // 10%, 20%, 30%, etc.
            
            // Create realistic test data
            var testCandidates = new List<DocumentCandidate>();
            for (int i = 0; i < counter; i++)
            {
                var candidate = new DocumentCandidate
                {
                    Id = Guid.NewGuid(),
                    Subject = $"Rechnung_{i + 1}_2024.pdf",
                    Sender = $"firma{i + 1}@example.com",
                    AccountName = "gmail@gmail.com",
                    FolderName = "INBOX",
                    AttachmentName = $"Rechnung_{i + 1}_2024.pdf",
                    ReceivedAt = DateTime.Now.AddDays(-i * 5),
                    Status = DocumentCandidateStatus.Pending
                };
                testCandidates.Add(candidate);
            }
            
            // Force immediate UI update
            ReplaceCandidates(testCandidates);
            scanLogger.LogInfo($"[TEST-REAL] {counter} realistische Test-Einträge hinzugefügt (sofort)");
            
            // Force UI refresh
            Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Render, () => {
                scanLogger.LogInfo($"[TEST-REAL-RENDER] UI Refresh erzwungen");
            });
            
            if (counter >= 10)
            {
                testTimer.Stop();
                SetBusyState(false);
                scanLogger.LogInfo("[TEST-REAL] Realistischer Live-Update Test beendet");
            }
        };
        testTimer.Start();
    }

    // Test method to check DataGrid binding and visibility
    public void CheckDataGridStatus()
    {
        scanLogger.LogInfo("[GRID-CHECK] Prüfe DataGrid Status...");
        
        // Check if CandidatesGrid exists
        if (CandidatesGrid == null)
        {
            scanLogger.LogError("[GRID-CHECK] CandidatesGrid ist NULL!");
            return;
        }
        
        // Check visibility
        scanLogger.LogInfo($"[GRID-CHECK] DataGrid Visibility: {CandidatesGrid.Visibility}");
        scanLogger.LogInfo($"[GRID-CHECK] DataGrid IsEnabled: {CandidatesGrid.IsEnabled}");
        scanLogger.LogInfo($"[GRID-CHECK] DataGrid ItemsSource: {CandidatesGrid.ItemsSource?.GetType().Name ?? "NULL"}");
        
        // Check Candidates collection
        var candidatesCount = Candidates?.Count ?? 0;
        scanLogger.LogInfo($"[GRID-CHECK] Candidates Collection Count: {candidatesCount}");
        
        // Check if items are in the grid
        var gridItemsCount = CandidatesGrid.Items.Count;
        scanLogger.LogInfo($"[GRID-CHECK] DataGrid Items Count: {gridItemsCount}");
        
        // Check if grid is actually visible on screen
        var actualHeight = CandidatesGrid.ActualHeight;
        var actualWidth = CandidatesGrid.ActualWidth;
        scanLogger.LogInfo($"[GRID-CHECK] DataGrid Actual Size: {actualWidth} x {actualHeight}");
        
        // Force grid refresh
        CandidatesGrid.Items.Refresh();
        scanLogger.LogInfo("[GRID-CHECK] Items.Refresh() aufgerufen");
        
        // Test with immediate update
        var testCandidate = new DocumentCandidate
        {
            Id = Guid.NewGuid(),
            Subject = "GRID-CHECK-TEST",
            Sender = "gridtest@example.com",
            AccountName = "Grid-Test",
            FolderName = "Test",
            AttachmentName = "gridcheck.pdf",
            ReceivedAt = DateTime.Now,
            Status = DocumentCandidateStatus.Pending
        };
        
        var testList = new List<DocumentCandidate> { testCandidate };
        ReplaceCandidates(testList);
        
        scanLogger.LogInfo("[GRID-CHECK] Test-Eintrag hinzugefügt - sollte sofort sichtbar sein");
        scanLogger.LogInfo($"[GRID-CHECK] Nach Update - Candidates Count: {Candidates?.Count ?? 0}");
        scanLogger.LogInfo($"[GRID-CHECK] Nach Update - Grid Items Count: {CandidatesGrid.Items.Count}");
    }

    // Test method to check filter settings
    public void CheckFilterSettings()
    {
        scanLogger.LogInfo("[FILTER-CHECK] Prüfe Filter-Einstellungen...");
        
        // Check current filter settings
        scanLogger.LogInfo($"[FILTER-CHECK] OnlyWithAttachments: {OnlyWithAttachments}");
        scanLogger.LogInfo($"[FILTER-CHECK] OnlyDocPdf: {OnlyDocPdf}");
        scanLogger.LogInfo($"[FILTER-CHECK] SearchText: '{SearchText}'");
        
        // Test with all candidates from database
        try
        {
            var allCandidates = documentCandidateStore.SearchAsync("").Result;
            scanLogger.LogInfo($"[FILTER-CHECK] Alle Kandidaten in DB: {allCandidates.Count}");
            
            // Check how many have attachments
            var withAttachments = allCandidates.Where(c => !c.AttachmentName.Equals("[Email-Text]", StringComparison.OrdinalIgnoreCase)).ToList();
            scanLogger.LogInfo($"[FILTER-CHECK] Mit Anhängen: {withAttachments.Count}");
            
            // Check how many are PDF/DOC
            var docPdf = allCandidates.Where(c => 
            {
                var name = c.AttachmentName.ToLower();
                return name.EndsWith(".pdf") || name.EndsWith(".doc") || name.EndsWith(".docx");
            }).ToList();
            scanLogger.LogInfo($"[FILTER-CHECK] PDF/DOC: {docPdf.Count}");
            
            // Check how many would pass both filters
            var bothFilters = allCandidates.Where(c => 
            {
                var name = c.AttachmentName.ToLower();
                var hasAttachment = !c.AttachmentName.Equals("[Email-Text]", StringComparison.OrdinalIgnoreCase);
                var isDocPdf = name.EndsWith(".pdf") || name.EndsWith(".doc") || name.EndsWith(".docx");
                return hasAttachment && isDocPdf;
            }).ToList();
            scanLogger.LogInfo($"[FILTER-CHECK] Beide Filter: {bothFilters.Count}");
            
            // Show some sample attachment names
            var sampleAttachments = allCandidates.Take(10).Select(c => c.AttachmentName).ToList();
            scanLogger.LogInfo($"[FILTER-CHECK] Beispiel-Anhänge: {string.Join(", ", sampleAttachments)}");
            
        }
        catch (Exception ex)
        {
            scanLogger.LogError($"[FILTER-CHECK] Fehler: {ex.Message}", ex);
        }
    }

    // Test method to check if grid is responsive
    public void TestGridUpdate()
    {
        scanLogger.LogInfo("[TEST] Teste Grid-Update...");
        
        // Check current count
        var currentCount = Candidates?.Count ?? 0;
        scanLogger.LogInfo($"[TEST] Aktuelle Treffer: {currentCount}");
        
        // Add one test item
        var testCandidate = new DocumentCandidate
        {
            Id = Guid.NewGuid(),
            Subject = "GRID-TEST",
            Sender = "test@example.com",
            AccountName = "Test",
            FolderName = "Test",
            AttachmentName = "test.pdf",
            ReceivedAt = DateTime.Now,
            Status = DocumentCandidateStatus.Pending
        };
        
        var testCandidates = new List<DocumentCandidate> { testCandidate };
        
        ReplaceCandidates(testCandidates);
        scanLogger.LogInfo("[TEST] Test-Eintrag hinzugefügt - sollte sofort sichtbar sein");
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

    private void OnAccountSettingsClicked(object sender, RoutedEventArgs e)
    {
        LoadAccountEditorSettings();
        SetCurrentPage(WorkspacePage.Accounts);
        StatusMessage = "Kontenansicht geoeffnet.";
    }

    private void OnUpdateClicked(object sender, RoutedEventArgs e)
    {
        SetCurrentPage(WorkspacePage.Update);
    }

    private void OnViewClicked(object sender, RoutedEventArgs e)
    {
        var selectedCandidates = CandidatesGrid.SelectedItems.Cast<CandidateListItem>().Select(item => item.Candidate).ToArray();
        if (selectedCandidates.Length == 0)
        {
            StatusMessage = "Bitte zuerst Dokumente in der Liste auswählen.";
            return;
        }

        if (selectedCandidates.Length > 1)
        {
            StatusMessage = "Bitte nur ein Dokument zum Anzeigen auswählen.";
            return;
        }

        var candidate = selectedCandidates[0];
        try
        {
            ShowPreview(candidate);
        }
        catch (Exception ex)
        {
            StatusMessage = $"Fehler beim Öffnen: {ex.Message}";
        }
    }

    private void OnDeleteClicked(object sender, RoutedEventArgs e)
    {
        var selectedCandidates = CandidatesGrid.SelectedItems.Cast<CandidateListItem>().Select(item => item.Candidate).ToArray();
        if (selectedCandidates.Length == 0)
        {
            StatusMessage = "Bitte zuerst Dokumente in der Liste auswählen.";
            return;
        }

        var result = System.Windows.MessageBox.Show(
            $"Möchtest du wirklich {selectedCandidates.Length} Dokument(e) löschen?\n\nDiese Aktion kann nicht rückgängig gemacht werden.",
            "Dokumente löschen",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (result != MessageBoxResult.Yes)
        {
            return;
        }

        try
        {
            var deletedCount = 0;
            foreach (var candidate in selectedCandidates)
            {
                // Delete from database
                documentCandidateStore.DeleteAsync(candidate.Id).Wait();
                deletedCount++;
            }

            // Refresh the list
            var currentCandidates = documentCandidateStore.SearchAsync(SearchText).Result;
            ReplaceCandidates(currentCandidates);

            StatusMessage = $"{deletedCount} Dokument(e) erfolgreich gelöscht.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Fehler beim Löschen: {ex.Message}";
        }
    }

    private void OnCandidatesSelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        var selectedCount = CandidatesGrid.SelectedItems.Count;
        if (selectedCount == 0)
        {
            SelectionInfo = "";
        }
        else if (selectedCount == 1)
        {
            SelectionInfo = $"1 Dokument ausgewählt";
        }
        else
        {
            SelectionInfo = $"{selectedCount} Dokumente ausgewählt";
        }
    }

    private void OnCandidatesMouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        var selectedCandidate = CandidatesGrid.SelectedItem as CandidateListItem;
        if (selectedCandidate == null) return;

        try
        {
            ShowPreview(selectedCandidate.Candidate);
        }
        catch (Exception ex)
        {
            StatusMessage = $"Fehler beim Öffnen der Vorschau: {ex.Message}";
        }
    }

    private async Task RefreshAsync()
    {
        // Cancel any previous scan
        cancellationTokenSource?.Cancel();
        cancellationTokenSource = new CancellationTokenSource();
        
        SetBusyState(true, "Suche nach neuen E-Mails mit Anhängen. Das kann je nach Postfach kurz dauern...");
        var progress = new Progress<MailImportProgress>(UpdateImportProgress);
        scanLogger.LogInfo("=== NEUER SCAN GESTARTET ===");

        try
        {
            StatusMessage = "Pruefe IMAP-Konten und gleiche mit SQLite ab...";
            LiveScanInfo = "Lade Konto-Einstellungen...";
            AccountSettingsInfo = LoadAccountSettingsInfo();
            
            scanLogger.LogInfo("Starte Import aller E-Mails...");
            
            var candidates = await mailImportService.ImportNewCandidatesAsync(progress, cancellationTokenSource.Token);
            
            // Update progress to 100% when complete
            ScanProgressPercentage = 100;
            LiveScanInfo = "Speichere Ergebnisse...";
            
            // Store candidates and update UI
            await documentCandidateStore.UpsertAsync(candidates, cancellationTokenSource.Token);
            
            // Apply filters and show results immediately
            var filteredCandidates = candidates.AsEnumerable();
            if (OnlyWithAttachments)
            {
                filteredCandidates = filteredCandidates.Where(c => !c.AttachmentName.Equals("[Email-Text]", StringComparison.OrdinalIgnoreCase));
            }
            if (OnlyDocPdf)
            {
                var allowedExtensions = new[] { ".pdf", ".doc", ".docx" };
                filteredCandidates = filteredCandidates.Where(c => 
                    allowedExtensions.Any(ext => c.AttachmentName.EndsWith(ext, StringComparison.OrdinalIgnoreCase)));
            }
            
            ReplaceCandidates(filteredCandidates);
            
            StatusMessage = $"Scan abgeschlossen! {filteredCandidates.Count()} Dokumente gefunden.";
            LiveScanInfo = $"✅ Fertig! {filteredCandidates.Count()} Treffer gefunden";
            scanLogger.LogInfo($"=== SCAN ABGESCHLOSSEN: {filteredCandidates.Count()} Dokumente gefunden ===");
        }
        catch (OperationCanceledException)
        {
            StatusMessage = "Scan wurde abgebrochen.";
            LiveScanInfo = "⏹️ Scan abgebrochen";
            scanLogger.LogInfo("=== SCAN ABGEBROCHEN ===");
        }
        catch (Exception ex)
        {
            StatusMessage = $"Scan fehlgeschlagen: {SimplifyErrorMessage(ex.Message)}";
            LiveScanInfo = "❌ Scan fehlgeschlagen";
            scanLogger.LogError($"Scan-Fehler: {ex.Message}", ex);
        }
        finally
        {
            SetBusyState(false);
            cancellationTokenSource?.Dispose();
            cancellationTokenSource = null;
        }
    }

    private async Task ApplySearchAsync()
    {
        var allCandidates = await documentCandidateStore.SearchAsync(SearchText);
        
        // Apply filters
        var filteredCandidates = allCandidates.AsEnumerable();
        
        if (OnlyWithAttachments)
        {
            filteredCandidates = filteredCandidates.Where(c => !c.AttachmentName.Equals("[Email-Text]", StringComparison.OrdinalIgnoreCase));
        }
        
        if (OnlyDocPdf)
        {
            var allowedExtensions = new[] { ".pdf", ".doc", ".docx" };
            filteredCandidates = filteredCandidates.Where(c => 
                allowedExtensions.Any(ext => c.AttachmentName.EndsWith(ext, StringComparison.OrdinalIgnoreCase)));
        }
        
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
        
        // Force immediate UI refresh
        Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Render, () => {
            // Force grid to update
            CandidatesGrid.Items.Refresh();
            scanLogger.LogInfo($"[REPLACE] UI Refresh erzwungen - {Candidates.Count} Einträge");
        });
    }

    private void SetBusyState(bool isBusy, string? message = null)
    {
        BusyVisibility = isBusy ? Visibility.Visible : Visibility.Collapsed;
        BusyMessage = message ?? string.Empty;
        RefreshButton.IsEnabled = !isBusy;
        AccountButton.IsEnabled = !isBusy;
        ConnectionsButton.IsEnabled = !isBusy;
        DebugButton.IsEnabled = !isBusy;
        UpdateButton.IsEnabled = !isBusy;
        SearchBox.IsEnabled = !isBusy;
        // Keep grid enabled during scan so users can see results immediately
        // CandidatesGrid.IsEnabled = !isBusy;
        
        // Reset live scan info when starting/stopping
        if (isBusy)
        {
            LiveScanInfo = "Starte Scan...";
        }
        else
        {
            LiveScanInfo = "Scan abgeschlossen.";
        }
        
        // Manage live update timer
        if (isBusy)
        {
            // Start timer for periodic updates - faster interval
            liveUpdateTimer = new DispatcherTimer 
            { 
                Interval = TimeSpan.FromMilliseconds(200) // Update every 200ms
            };
            liveUpdateTimer.Tick += async (s, e) => await UpdateCandidatesDuringScanAsync();
            liveUpdateTimer.Start();
            scanLogger.LogInfo("[TIMER] Live-Update Timer gestartet (200ms)");
        }
        else
        {
            // Stop timer when scan completes
            if (liveUpdateTimer != null)
            {
                liveUpdateTimer.Stop();
                liveUpdateTimer = null;
                scanLogger.LogInfo("[TIMER] Live-Update Timer gestoppt");
            }
        }
        
        // Log state changes
        scanLogger.LogInfo($"[STATE] Busy={isBusy}, BusyVisibility={BusyVisibility}, StopButton should be {(isBusy ? "VISIBLE" : "HIDDEN")}");
    }

    private void UpdateImportProgress(MailImportProgress progress)
    {
        // Calculate overall percentage based on accounts and folders completed
        var accountProgress = progress.AccountsTotal > 0 
            ? (progress.AccountsCompleted * 100.0 / progress.AccountsTotal) 
            : 0;
        var folderProgress = (progress.AccountsTotal * progress.FoldersTotal) > 0 
            ? (progress.FoldersCompleted * 100.0 / (progress.AccountsTotal * progress.FoldersTotal)) 
            : 0;
        var overallPercentage = (accountProgress + folderProgress) / 2;

        ScanProgressPercentage = overallPercentage;
        CurrentScanTarget = $"Scanne {progress.AccountName}/{progress.FolderName}: {progress.MessagesScanned} Mails gescannt";
        
        // Enhanced live scan info
        var oldestAge = progress.OldestScannedMessageAgeDays;
        var oldestInfo = oldestAge >= 0 
            ? $"älteste Mail: vor {oldestAge} Tagen ({progress.OldestScannedMessageDate:dd.MM.yyyy})"
            : "noch keine Mail gescannt";
            
        LiveScanInfo = $"📧 {progress.AccountName} | 📁 {progress.FolderName} | 📅 {oldestInfo} | 📎 {progress.AttachmentMessagesFound} Anhänge | 📄 {progress.PdfCandidatesFound} Treffer";
        
        // Update account settings info with current account being scanned
        var currentAccountInfo = $"🔍 Aktuell: {progress.AccountName} ({progress.ConfiguredLookbackDays} Tage)";
        AccountSettingsInfo = currentAccountInfo;
        
        // Update metrics during scan
        AttachmentMailSummary = $"{progress.AttachmentMessagesFound} Mails mit Anhang";
        PdfCandidateSummary = $"{progress.PdfCandidatesFound} Dokumente";
        InvoiceMatchSummary = $"{progress.InvoiceMatchesFound} Rechnungen";

        OldestMailSummary = oldestAge >= 0 
            ? $"Aelteste gescannte Mail: vor {oldestAge} Tagen"
            : "Aelteste gescannte Mail: noch kein Scan";

        var statusText = $"Konto {progress.AccountsCompleted + 1}/{progress.AccountsTotal}, Ordner {progress.FoldersCompleted + 1}/{progress.FoldersTotal} - {progress.StatusText}";
        ScanProgressSummary = statusText;

        // Update lookback and excluded folders info
        var lookbackText = progress.IsFullScan
            ? progress.OldestScannedMessageAgeDays is int days
                ? $"Scanbereich: kompletter Verlauf, bisher ca. {days} Tage zurueck"
                : "Scanbereich: kompletter Verlauf"
            : $"Scanbereich: letzte {progress.ConfiguredLookbackDays} Tage";

        LookbackScopeSummary = lookbackText;
        ExcludedFoldersSummary = progress.ExcludedFolderCount == 0
            ? "Ausgeschlossene Ordner: keine"
            : $"Ausgeschlossene Ordner aktiv: {progress.ExcludedFolderCount}";

        // Log progress details
        scanLogger.LogInfo($"[PROGRESS] {statusText} - {progress.PdfCandidatesFound} Dokumente gefunden, {progress.AttachmentMessagesFound} Mails mit Anhang");

        // IMMEDIATE results update - always update, no conditions
        scanLogger.LogInfo($"[LIVE-TRIGGER] Update-Check: {progress.PdfCandidatesFound} PDFs, {progress.AttachmentMessagesFound} Anhänge");
        
        // Use synchronous BeginInvoke for immediate update
        Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Send, async () => {
            try
            {
                scanLogger.LogInfo("[LIVE-START] Beginne Live-Update...");
                
                // Get all current candidates from database
                var allCandidates = await documentCandidateStore.SearchAsync(SearchText);
                scanLogger.LogInfo($"[LIVE-DB] {allCandidates.Count} Kandidaten aus DB geladen");
                
                // Apply current filters
                var filteredCandidates = allCandidates.AsEnumerable();
                
                if (OnlyWithAttachments)
                {
                    var beforeCount = filteredCandidates.Count();
                    filteredCandidates = filteredCandidates.Where(c => !c.AttachmentName.Equals("[Email-Text]", StringComparison.OrdinalIgnoreCase));
                    scanLogger.LogInfo($"[LIVE-FILTER] Nur mit Anhang: {beforeCount} -> {filteredCandidates.Count()}");
                }
                
                if (OnlyDocPdf)
                {
                    var beforeCount = filteredCandidates.Count();
                    var allowedExtensions = new[] { ".pdf", ".doc", ".docx" };
                    filteredCandidates = filteredCandidates.Where(c => 
                        allowedExtensions.Any(ext => c.AttachmentName.EndsWith(ext, StringComparison.OrdinalIgnoreCase)));
                    scanLogger.LogInfo($"[LIVE-FILTER] Nur DOC/PDF: {beforeCount} -> {filteredCandidates.Count()}");
                }
                
                // Force UI update
                var finalCount = filteredCandidates.Count();
                ReplaceCandidates(filteredCandidates);
                
                // Log update
                scanLogger.LogInfo($"[LIVE-IMMEDIATE] Zeige {finalCount} Treffer an (sofort)");
                
                // Force UI refresh with higher priority
                Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Render, () => {
                    scanLogger.LogInfo($"[LIVE-RENDER] UI Refresh erzwungen");
                });
            }
            catch (Exception ex)
            {
                scanLogger.LogError($"Fehler beim Live-Update: {ex.Message}", ex);
            }
        });
    }

    private string LoadAccountSettingsInfo()
    {
        try
        {
            var settings = settingsProvider.GetCurrentSettings();
            var accounts = settings.MailImport.Accounts;
            
            if (accounts == null || accounts.Count == 0)
            {
                return "⚠️ Keine Konten konfiguriert";
            }

            var accountInfos = new List<string>();
            var daysText = settings.MailImport.InitialLookbackDays switch
            {
                0 => "kompletter Verlauf",
                1 => "letzter Tag",
                _ => $"letzte {settings.MailImport.InitialLookbackDays} Tage"
            };
            
            foreach (var account in accounts)
            {
                var statusText = "✅ Aktiv"; // Alle Konten sind standardmäßig aktiv
                var excludedText = account.ExcludedFolderPatterns?.Count > 0 
                    ? $" ({account.ExcludedFolderPatterns.Count} Ordner ausgeschlossen)" 
                    : "";
                
                accountInfos.Add($"{statusText} {account.DisplayName}: {daysText}{excludedText}");
            }
            
            var result = string.Join(" | ", accountInfos);
            scanLogger.LogInfo($"[ACCOUNTS] {result}");
            return result;
        }
        catch (Exception ex)
        {
            scanLogger.LogError($"Fehler beim Laden der Konto-Einstellungen: {ex.Message}", ex);
            return "⚠️ Fehler beim Laden der Einstellungen";
        }
    }

    private async Task UpdateCandidatesDuringScanAsync()
    {
        try
        {
            scanLogger.LogInfo("[TIMER-START] Timer-Update gestartet...");
            
            // Get all current candidates from database
            var allCandidates = await documentCandidateStore.SearchAsync(SearchText);
            scanLogger.LogInfo($"[TIMER-DB] {allCandidates.Count} Kandidaten aus DB geladen");
            
            // Apply filters
            var filteredCandidates = allCandidates.AsEnumerable();
            
            if (OnlyWithAttachments)
            {
                var beforeCount = filteredCandidates.Count();
                filteredCandidates = filteredCandidates.Where(c => !c.AttachmentName.Equals("[Email-Text]", StringComparison.OrdinalIgnoreCase));
                scanLogger.LogInfo($"[TIMER-FILTER] Nur mit Anhang: {beforeCount} -> {filteredCandidates.Count()}");
            }
            
            if (OnlyDocPdf)
            {
                var beforeCount = filteredCandidates.Count();
                var allowedExtensions = new[] { ".pdf", ".doc", ".docx" };
                filteredCandidates = filteredCandidates.Where(c => 
                    allowedExtensions.Any(ext => c.AttachmentName.EndsWith(ext, StringComparison.OrdinalIgnoreCase)));
                scanLogger.LogInfo($"[TIMER-FILTER] Nur DOC/PDF: {beforeCount} -> {filteredCandidates.Count()}");
            }
            
            // Update UI immediately
            ReplaceCandidates(filteredCandidates);
            
            // Log timer updates with count
            scanLogger.LogInfo($"[TIMER-200MS] Live-Update: {filteredCandidates.Count()} Treffer angezeigt");
        }
        catch (Exception ex)
        {
            scanLogger.LogError($"Fehler beim Timer-Update: {ex.Message}", ex);
        }
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
                LatestReleaseButtonText = $"Update auf {release.LatestVersion}";
                LatestVersionSummary = $"Neu verfuegbar: {release.LatestVersion}";
                UpdateStatusSummary = release.InstallerAsset is null
                    ? $"Neue GitHub-Release verfuegbar. Per Klick oeffnest du die Release-Seite."
                    : $"Neue Version mit Installer verfuegbar. Per Klick kannst du das Update laden.";
                SyncUpdatePanel(release);
                return;
            }

            LatestReleaseVisibility = Visibility.Collapsed;
            LatestReleaseButtonText = "Update";
            LatestVersionSummary = "Neueste Release installiert";
            UpdateStatusSummary = "GitHub-Release aktuell.";
            SyncUpdatePanel(release);
        }
        catch (Exception ex)
        {
            LatestReleaseVisibility = Visibility.Collapsed;
            LatestReleaseButtonText = "Update";
            LatestVersionSummary = "Release-Check derzeit nicht verfuegbar";
            UpdateStatusSummary = $"Release-Pruefung derzeit nicht verfuegbar: {ex.Message}";
            SyncUpdatePanel(GitHubReleaseUpdateService.ReleaseUpdateInfo.Unavailable() with
            {
                ReleaseTitle = "Release-Check derzeit nicht verfuegbar",
                ReleaseNotes = ex.Message
            });
        }
    }

    private static string SimplifyErrorMessage(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return "Unbekannter Fehler.";
        }

        return message.Contains("keine Daten des angeforderten Typs", StringComparison.OrdinalIgnoreCase)
            ? "Der Mailserver hat auf die Anfrage unerwartet geantwortet."
            : message;
    }

    private void OnOpenLatestReleaseClicked(object sender, RoutedEventArgs e)
    {
        SetCurrentPage(WorkspacePage.Update);
    }

    private void OnCopyLogClicked(object sender, RoutedEventArgs e)
    {
        try
        {
            System.Windows.Clipboard.SetText(scanLogger.GetLogText());
            StatusMessage = "Protokoll in Zwischenablage kopiert!";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Fehler beim Kopieren: {ex.Message}";
        }
    }

    private async void OnSaveLogClicked(object sender, RoutedEventArgs e)
    {
        try
        {
            await scanLogger.SaveLogAsync();
            LogFilePathText.Text = $"Gespeichert: {scanLogger.GetLogFilePath()}";
            StatusMessage = "Protokoll gespeichert!";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Fehler beim Speichern: {ex.Message}";
        }
    }

    private void OnClearLogClicked(object sender, RoutedEventArgs e)
    {
        scanLogger.LogChanged -= HandleScanLogChanged;
        scanLogger = new ScanLogger();
        scanLogger.LogChanged += HandleScanLogChanged;
        OnPropertyChanged(nameof(LogText));
        LogFilePathText.Text = "";
        StatusMessage = "Protokoll gelöscht!";
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
