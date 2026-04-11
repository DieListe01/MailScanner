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
    private readonly IMailPreviewService mailPreviewService;
    private readonly IMailConnectionTestService mailConnectionTestService;
    private readonly IDocumentCandidateStore documentCandidateStore;
    private readonly IDocumentDownloadService documentDownloadService;
    private readonly AppVersionService appVersionService;
    private readonly GitHubReleaseUpdateService releaseUpdateService;
    private string accountCountSummary = "0 Konten";
    private string busyMessage = string.Empty;
    private Visibility busyVisibility = Visibility.Collapsed;
    private Visibility stopScanVisibility = Visibility.Collapsed;
    private string currentVersionSummary = string.Empty;
    private string currentScanTarget = string.Empty;
    private System.Windows.Media.Brush scanProgressBrush = System.Windows.Media.Brushes.DodgerBlue;
    private string liveScanInfo = "Bereit zum Scannen...";
    private string scanPhaseLabel = "Bereit";
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
    private bool onlyDocPdf = true;
    private bool onlyInvoices = true;
    private bool onlyDownloaded;
    private bool onlyMissingFiles;
    private string senderFilterText = string.Empty;
    private string accountFilterText = string.Empty;
    private string dashboardSearchText = string.Empty;
    private string scannedMailCountSummary = "Noch keine Mails gescannt";
    private string downloadActionLabel = "Herunterladen";
    private string updateStatusSummary = "Pruefe GitHub-Releases nach dem Start...";
    private ScanLogger scanLogger = new();
    private CancellationTokenSource? cancellationTokenSource;
    private DispatcherTimer? liveUpdateTimer;
    private bool canPreviewSelection;
    private bool canOpenSelectedFile;
    private bool canDownloadSelection;
    private bool canDeleteSelection;
    private ResultsQuickFilter resultsQuickFilter = ResultsQuickFilter.Invoices;
    private bool autoDownloadAfterScan;
    private Visibility scanNotificationVisibility = Visibility.Collapsed;
    private string scanNotificationText = string.Empty;
    private DispatcherTimer? notificationTimer;
    private int previewLoadVersion;

    public MainWindow(
        IAppSettingsProvider settingsProvider,
        IMailImportService mailImportService,
        IMailPreviewService mailPreviewService,
        IMailConnectionTestService mailConnectionTestService,
        IDocumentCandidateStore documentCandidateStore,
        IDocumentDownloadService documentDownloadService,
        ScanLogger scanLogger,
        AppVersionService appVersionService,
        GitHubReleaseUpdateService releaseUpdateService)
    {
        this.settingsProvider = settingsProvider;
        this.mailImportService = mailImportService;
        this.mailPreviewService = mailPreviewService;
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
        Loaded += (_, _) => UpdateResultsFilterButtons();
        var currentVersion = appVersionService.GetCurrentVersion();
        Title = $"MailScanner v{currentVersion}";
        CurrentVersionSummary = $"Installiert: v{currentVersion}";
        RefreshExcludedFolderSummary();
        RefreshLookbackSummary();
        RefreshAccountSummary();
    }

    private void HandleScanLogChanged()
    {
        Dispatcher.Invoke(() =>
        {
            OnPropertyChanged(nameof(LogText));
            OnPropertyChanged(nameof(CompactLogText));
            OnPropertyChanged(nameof(DashboardRecentLogText));
        });
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
            OnPropertyChanged(nameof(ResultsHeaderVisibility));
            OnPropertyChanged(nameof(ResultsScanStatusVisibility));
            OnPropertyChanged(nameof(ResultsSupportPanelVisibility));
            OnPropertyChanged(nameof(ResultsRightColumnWidth));
        }
    }

    public Visibility StopScanVisibility
    {
        get => stopScanVisibility;
        set
        {
            stopScanVisibility = value;
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

    public string ScanPhaseLabel
    {
        get => scanPhaseLabel;
        set
        {
            scanPhaseLabel = value;
            OnPropertyChanged();
        }
    }

    public System.Windows.Media.Brush ScanProgressBrush
    {
        get => scanProgressBrush;
        set
        {
            scanProgressBrush = value;
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
            OnPropertyChanged(nameof(OldestMailValue));
        }
    }

    public string OldestMailValue => OldestMailSummary.StartsWith("Aelteste gescannte Mail: ", StringComparison.Ordinal)
        ? OldestMailSummary["Aelteste gescannte Mail: ".Length..]
        : OldestMailSummary;

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
            OnPropertyChanged(nameof(LookbackScopeValue));
        }
    }

    public string LookbackScopeValue => LookbackScopeSummary.StartsWith("Scanbereich: ", StringComparison.Ordinal)
        ? LookbackScopeSummary["Scanbereich: ".Length..]
        : LookbackScopeSummary;

    public string ExcludedFoldersSummary
    {
        get => excludedFoldersSummary;
        set
        {
            excludedFoldersSummary = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(ExcludedFoldersValue));
        }
    }

    public string ExcludedFoldersValue => ExcludedFoldersSummary.StartsWith("Ausgeschlossene Ordner: ", StringComparison.Ordinal)
        ? ExcludedFoldersSummary["Ausgeschlossene Ordner: ".Length..]
        : ExcludedFoldersSummary;

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
            OnPropertyChanged(nameof(CurrentResultsFilterLabel));
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
            OnPropertyChanged(nameof(CurrentResultsFilterLabel));
            _ = ApplySearchAsync();
        }
    }

    public bool OnlyInvoices
    {
        get => onlyInvoices;
        set
        {
            onlyInvoices = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(CurrentResultsFilterLabel));
            _ = ApplySearchAsync();
        }
    }

    public bool OnlyDownloaded
    {
        get => onlyDownloaded;
        set
        {
            onlyDownloaded = value;
            if (value)
            {
                OnlyMissingFiles = false;
            }

            OnPropertyChanged();
            OnPropertyChanged(nameof(CurrentResultsFilterLabel));
            _ = ApplySearchAsync();
        }
    }

    public bool OnlyMissingFiles
    {
        get => onlyMissingFiles;
        set
        {
            onlyMissingFiles = value;
            if (value)
            {
                OnlyDownloaded = false;
            }

            OnPropertyChanged();
            OnPropertyChanged(nameof(CurrentResultsFilterLabel));
            _ = ApplySearchAsync();
        }
    }

    public string SenderFilterText
    {
        get => senderFilterText;
        set
        {
            if (senderFilterText == value)
            {
                return;
            }

            senderFilterText = value;
            OnPropertyChanged();
            _ = ApplySearchAsync();
        }
    }

    public string AccountFilterText
    {
        get => accountFilterText;
        set
        {
            if (accountFilterText == value)
            {
                return;
            }

            accountFilterText = value;
            OnPropertyChanged();
            _ = ApplySearchAsync();
        }
    }

    public string DashboardSearchText
    {
        get => dashboardSearchText;
        set
        {
            if (dashboardSearchText == value)
            {
                return;
            }

            dashboardSearchText = value;
            OnPropertyChanged();
        }
    }

    public string DashboardDocumentCountLabel => Candidates.Count(item => !item.AttachmentName.Equals("[Email-Text]", StringComparison.OrdinalIgnoreCase)) switch
    {
        1 => "1 Dokument",
        var count => $"{count} Dokumente"
    };

    public string DashboardInvoiceCountLabel => Candidates.Count(item =>
        !item.AttachmentName.Equals("[Email-Text]", StringComparison.OrdinalIgnoreCase)
        && item.Candidate.SuggestedCategory == DocumentCategory.Invoice) switch
    {
        1 => "1 Rechnung",
        var count => $"{count} Rechnungen"
    };

    public string DashboardMissingCountLabel => Candidates.Count(item =>
        !item.AttachmentName.Equals("[Email-Text]", StringComparison.OrdinalIgnoreCase)
        && item.FileAvailabilityLabel == "Datei fehlt") switch
    {
        1 => "1 fehlende Datei",
        var count => $"{count} fehlende Dateien"
    };

    public string ScannedMailCountSummary
    {
        get => scannedMailCountSummary;
        set
        {
            scannedMailCountSummary = value;
            OnPropertyChanged();
        }
    }

    public string CurrentResultsFilterLabel
    {
        get
        {
            if (OnlyMissingFiles)
            {
                return "Aktiver Filter: Datei fehlt";
            }

            if (resultsQuickFilter == ResultsQuickFilter.Downloaded || OnlyDownloaded)
            {
                return "Aktiver Filter: Downloads";
            }

            if (resultsQuickFilter == ResultsQuickFilter.Invoices || OnlyInvoices)
            {
                return "Aktiver Filter: Rechnungen";
            }

            if (OnlyWithAttachments && !OnlyDocPdf)
            {
                return "Aktiver Filter: Mit Anhang";
            }

            if (OnlyDocPdf)
            {
                return "Aktiver Filter: Dokumente";
            }

            return "Aktiver Filter: Alle Treffer";
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

    public string CompactLogText => scanLogger.GetRecentLogText(10);
    public IEnumerable<CandidateListItem> DashboardRecentCandidates => Candidates.Take(5);
    public string DashboardRecentLogText => scanLogger.GetRecentLogText(6);

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

    public bool CanPreviewSelection { get => canPreviewSelection; set { canPreviewSelection = value; OnPropertyChanged(); } }
    public bool CanOpenSelectedFile { get => canOpenSelectedFile; set { canOpenSelectedFile = value; OnPropertyChanged(); } }
    public bool CanDownloadSelection { get => canDownloadSelection; set { canDownloadSelection = value; OnPropertyChanged(); } }
    public bool CanDeleteSelection { get => canDeleteSelection; set { canDeleteSelection = value; OnPropertyChanged(); } }
    public string DownloadActionLabel { get => downloadActionLabel; set { downloadActionLabel = value; OnPropertyChanged(); } }
    public bool AutoDownloadAfterScan { get => autoDownloadAfterScan; set { autoDownloadAfterScan = value; OnPropertyChanged(); } }
    public Visibility ScanNotificationVisibility { get => scanNotificationVisibility; set { scanNotificationVisibility = value; OnPropertyChanged(); } }
    public string ScanNotificationText { get => scanNotificationText; set { scanNotificationText = value; OnPropertyChanged(); } }

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

    private void ShowScanNotification(string message)
    {
        ScanNotificationText = message;
        ScanNotificationVisibility = Visibility.Visible;

        notificationTimer?.Stop();
        notificationTimer ??= new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
        notificationTimer.Tick -= OnNotificationTimerTick;
        notificationTimer.Tick += OnNotificationTimerTick;
        notificationTimer.Start();
    }

    private void OnNotificationTimerTick(object? sender, EventArgs e)
    {
        notificationTimer?.Stop();
        ScanNotificationVisibility = Visibility.Collapsed;
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
        var selectedCandidates = CandidatesGrid.SelectedItems.Cast<CandidateListItem>()
            .Select(item => item.Candidate)
            .Where(candidate => !candidate.AttachmentName.Equals("[Email-Text]", StringComparison.OrdinalIgnoreCase) && !IsCandidateLocallyAvailable(candidate))
            .ToArray();

        if (selectedCandidates.Length == 0)
        {
            StatusMessage = "Die Auswahl ist bereits lokal vorhanden oder enthaelt keine herunterladbaren Dateien.";
            return;
        }

        SetBusyState(true, $"Speichere {selectedCandidates.Length} ausgewaehlte Dokumente...");

        try
        {
            StatusMessage = $"Lade {selectedCandidates.Length} Dokumente herunter...";
            var manualDownloadProgress = new Progress<DocumentDownloadProgress>(downloadProgress =>
            {
                StatusMessage = downloadProgress.HasError
                    ? $"Download {downloadProgress.CompletedCount} / {downloadProgress.TotalCount}: Fehler bei {downloadProgress.CurrentFileName}"
                    : $"Download {downloadProgress.CompletedCount} / {downloadProgress.TotalCount}: {downloadProgress.CurrentFileName}";
            });
            var result = await documentDownloadService.DownloadAsync(selectedCandidates, manualDownloadProgress);
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
            _ = ShowPreviewAsync(candidate);
        }
        catch (Exception ex)
        {
            StatusMessage = $"Fehler beim Öffnen: {ex.Message}";
        }
    }

    private void OnOpenSelectedFileClicked(object sender, RoutedEventArgs e)
    {
        var selectedCandidates = CandidatesGrid.SelectedItems.Cast<CandidateListItem>().Select(item => item.Candidate).ToArray();
        if (selectedCandidates.Length == 0)
        {
            StatusMessage = "Bitte zuerst einen Treffer auswaehlen.";
            return;
        }

        if (selectedCandidates.Length > 1)
        {
            StatusMessage = "Bitte nur einen Treffer zum Oeffnen auswaehlen.";
            return;
        }

        OpenCandidateAttachment(selectedCandidates[0]);
    }

    private void OnRevealSelectedFileClicked(object sender, RoutedEventArgs e)
    {
        var selectedCandidates = CandidatesGrid.SelectedItems.Cast<CandidateListItem>().Select(item => item.Candidate).ToArray();
        if (selectedCandidates.Length == 0)
        {
            StatusMessage = "Bitte zuerst einen Treffer auswaehlen.";
            return;
        }

        if (selectedCandidates.Length > 1)
        {
            StatusMessage = "Bitte nur einen Treffer zum Anzeigen im Ordner auswaehlen.";
            return;
        }

        RevealCandidateAttachment(selectedCandidates[0]);
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
        var selectedItems = CandidatesGrid.SelectedItems.Cast<CandidateListItem>().ToArray();
        var selectedCount = selectedItems.Length;
        CanPreviewSelection = selectedCount == 1;
        CanOpenSelectedFile = false;
        CanDownloadSelection = false;
        CanDeleteSelection = selectedCount >= 1;
        DownloadActionLabel = "Herunterladen";

        if (selectedCount == 0)
        {
            SelectionInfo = "";
            OnPropertyChanged(nameof(CanOpenPreviewAttachment));
            OnPropertyChanged(nameof(CanDownloadPreviewAttachment));
        }
        else if (selectedCount == 1)
        {
            var selectedItem = selectedItems[0];
            var hasLocalFile = IsCandidateLocallyAvailable(selectedItem.Candidate);
            SelectionInfo = hasLocalFile ? "1 Dokument ausgewählt, lokal verfügbar" : "1 Dokument ausgewählt, Datei fehlt lokal";
            if (CandidatesGrid.SelectedItem is CandidateListItem)
            {
                CanOpenSelectedFile = hasLocalFile;
                CanDownloadSelection = !selectedItem.Candidate.AttachmentName.Equals("[Email-Text]", StringComparison.OrdinalIgnoreCase) && !hasLocalFile;
                DownloadActionLabel = CanDownloadSelection ? "Herunterladen" : "Bereits lokal";
                _ = ShowPreviewAsync(selectedItem.Candidate, false);
            }
        }
        else
        {
            var downloadableCount = selectedItems.Count(item => !item.Candidate.AttachmentName.Equals("[Email-Text]", StringComparison.OrdinalIgnoreCase) && !IsCandidateLocallyAvailable(item.Candidate));
            var localCount = selectedItems.Count(item => IsCandidateLocallyAvailable(item.Candidate));
            CanDownloadSelection = downloadableCount > 0;
            DownloadActionLabel = downloadableCount == 0 ? "Bereits lokal" : localCount > 0 ? "Fehlende laden" : "Herunterladen";
            SelectionInfo = $"{selectedCount} Dokumente ausgewählt, {downloadableCount} downloadbar";
            OnPropertyChanged(nameof(CanOpenPreviewAttachment));
            OnPropertyChanged(nameof(CanDownloadPreviewAttachment));
        }
    }

    private void OnCandidatesMouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        var selectedCandidate = CandidatesGrid.SelectedItem as CandidateListItem;
        if (selectedCandidate == null) return;

        try
        {
            _ = ShowPreviewAsync(selectedCandidate.Candidate);
            OpenCandidateAttachment(selectedCandidate.Candidate);
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
        PreviewTabVisibility = Visibility.Collapsed;
        SetCurrentPage(WorkspacePage.Results);
        var progress = new Progress<MailImportProgress>(UpdateImportProgress);
        scanLogger.LogInfo("=== NEUER SCAN GESTARTET ===");

        try
        {
            StatusMessage = "Pruefe IMAP-Konten und gleiche mit SQLite ab...";
            ScanPhaseLabel = "IMAP-Scan";
            ScanProgressBrush = (System.Windows.Media.Brush)FindResource("AccentBrush");
            LiveScanInfo = "Lade Konto-Einstellungen...";
            AccountSettingsInfo = LoadAccountSettingsInfo();
            
            scanLogger.LogInfo("Starte Import aller E-Mails...");
            
            var candidates = await mailImportService.ImportNewCandidatesAsync(progress, cancellationTokenSource.Token);

            StopLiveUpdateTimer();
            StopScanVisibility = Visibility.Collapsed;
            ScanPhaseLabel = "Nachverarbeitung";
            ScanProgressBrush = (System.Windows.Media.Brush)FindResource("AmberBrush");
            ScanProgressPercentage = Math.Max(ScanProgressPercentage, 96);
            LiveScanInfo = "IMAP-Scan abgeschlossen. Speichere Ergebnisse...";
            ScanProgressSummary = "Nachverarbeitung: Treffer werden gespeichert und aktualisiert";
            
            // Store candidates and update UI
            await documentCandidateStore.UpsertAsync(candidates, cancellationTokenSource.Token);
            
            // Apply filters and show results immediately
            var filteredCandidates = ApplyCandidateFilters(candidates);
            
            var filteredCandidateList = filteredCandidates.ToList();
            ReplaceCandidates(filteredCandidateList);

            if (AutoDownloadAfterScan && filteredCandidateList.Count > 0)
            {
                LiveScanInfo = $"IMAP-Scan abgeschlossen. Lade {filteredCandidateList.Count} Treffer herunter...";
                ScanProgressSummary = "Nachverarbeitung: Auto-Download laeuft";
                scanLogger.LogInfo($"[AUTO-DOWNLOAD] Lade {filteredCandidateList.Count} Treffer direkt herunter...");
                var autoDownloadProgress = new Progress<DocumentDownloadProgress>(downloadProgress =>
                {
                    LiveScanInfo = $"Auto-Download: {downloadProgress.CompletedCount} / {downloadProgress.TotalCount} - {downloadProgress.CurrentFileName}";
                    ScanProgressSummary = downloadProgress.HasError
                        ? $"Nachverarbeitung: {downloadProgress.CompletedCount} / {downloadProgress.TotalCount} mit Fehlern"
                        : $"Nachverarbeitung: {downloadProgress.CompletedCount} / {downloadProgress.TotalCount} geladen";

                    if (downloadProgress.TotalCount > 0)
                    {
                        ScanProgressPercentage = 96 + (downloadProgress.CompletedCount * 4.0 / downloadProgress.TotalCount);
                    }
                });
                var autoDownloadResult = await documentDownloadService.DownloadAsync(filteredCandidateList, autoDownloadProgress, cancellationTokenSource.Token);
                if (autoDownloadResult.Errors.Count > 0)
                {
                    foreach (var error in autoDownloadResult.Errors)
                    {
                        scanLogger.LogError($"[AUTO-DOWNLOAD] {error}");
                    }
                }

                var refreshedCandidates = await documentCandidateStore.SearchAsync(SearchText);
                ReplaceCandidates(ApplyCandidateFilters(refreshedCandidates).ToList());

                StatusMessage = autoDownloadResult.Errors.Count == 0
                    ? $"Scan abgeschlossen! {filteredCandidateList.Count} Dokumente gefunden, {autoDownloadResult.DownloadedDocuments.Count} direkt heruntergeladen."
                    : $"Scan abgeschlossen! {filteredCandidateList.Count} Dokumente gefunden, {autoDownloadResult.DownloadedDocuments.Count} heruntergeladen, {autoDownloadResult.Errors.Count} Fehler.";
            }
            else
            {
                StatusMessage = $"Scan abgeschlossen! {filteredCandidateList.Count} Dokumente gefunden.";
            }

            ScanProgressPercentage = 100;
            ScanPhaseLabel = "Abgeschlossen";
            ScanProgressBrush = (System.Windows.Media.Brush)FindResource("GreenBrush");
            LiveScanInfo = $"Fertig! {filteredCandidateList.Count} Treffer gefunden";
            ScanProgressSummary = $"Abgeschlossen: {filteredCandidateList.Count} Treffer bereit";
            if (!string.IsNullOrWhiteSpace(CurrentScanTarget))
            {
                ScannedMailCountSummary = CurrentScanTarget.Replace("Scanne ", string.Empty, StringComparison.Ordinal);
            }
            scanLogger.LogInfo($"=== SCAN ABGESCHLOSSEN: {filteredCandidateList.Count} Dokumente gefunden ===");
            SetCurrentPage(WorkspacePage.Results);
            PreviewTabVisibility = Visibility.Collapsed;
            ShowScanNotification($"Scan fertig: {filteredCandidateList.Count} Treffer gefunden");
        }
        catch (OperationCanceledException)
        {
            StatusMessage = "Scan wurde abgebrochen.";
            ScanPhaseLabel = "Abgebrochen";
            ScanProgressBrush = (System.Windows.Media.Brush)FindResource("RedBrush");
            LiveScanInfo = "⏹️ Scan abgebrochen";
            scanLogger.LogInfo("=== SCAN ABGEBROCHEN ===");
        }
        catch (Exception ex)
        {
            StatusMessage = $"Scan fehlgeschlagen: {SimplifyErrorMessage(ex.Message)}";
            ScanPhaseLabel = "Fehler";
            ScanProgressBrush = (System.Windows.Media.Brush)FindResource("RedBrush");
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
        var filteredCandidates = ApplyCandidateFilters(allCandidates);
        
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

    private IEnumerable<DocumentCandidate> ApplyCandidateFilters(IEnumerable<DocumentCandidate> candidates)
    {
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

        if (OnlyInvoices || resultsQuickFilter == ResultsQuickFilter.Invoices)
        {
            filteredCandidates = filteredCandidates.Where(c => c.SuggestedCategory == DocumentCategory.Invoice);
        }

        if (OnlyDownloaded || resultsQuickFilter == ResultsQuickFilter.Downloaded)
        {
            filteredCandidates = filteredCandidates.Where(IsCandidateLocallyAvailable);
        }

        if (OnlyMissingFiles)
        {
            filteredCandidates = filteredCandidates.Where(c => !c.AttachmentName.Equals("[Email-Text]", StringComparison.OrdinalIgnoreCase) && !IsCandidateLocallyAvailable(c));
        }

        if (!string.IsNullOrWhiteSpace(AccountFilterText))
        {
            var accountTerm = AccountFilterText.Trim();
            filteredCandidates = filteredCandidates.Where(c =>
                c.AccountName.Contains(accountTerm, StringComparison.OrdinalIgnoreCase)
                || c.AccountAddress.Contains(accountTerm, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(SenderFilterText))
        {
            var senderTerm = SenderFilterText.Trim();
            filteredCandidates = filteredCandidates.Where(c => c.Sender.Contains(senderTerm, StringComparison.OrdinalIgnoreCase));
        }

        return filteredCandidates;
    }

    private static bool IsCandidateLocallyAvailable(DocumentCandidate candidate)
    {
        return !candidate.AttachmentName.Equals("[Email-Text]", StringComparison.OrdinalIgnoreCase)
            && (candidate.AlreadyDownloaded || ResolveCandidateAttachmentPath(candidate) is not null);
    }

    private void OnShowAllResultsClicked(object sender, RoutedEventArgs e)
    {
        resultsQuickFilter = ResultsQuickFilter.All;
        OnlyInvoices = false;
        OnlyDownloaded = false;
        UpdateResultsFilterButtons();
        _ = ApplySearchAsync();
    }

    private void OnShowInvoicesClicked(object sender, RoutedEventArgs e)
    {
        resultsQuickFilter = ResultsQuickFilter.Invoices;
        OnlyInvoices = true;
        OnlyDownloaded = false;
        UpdateResultsFilterButtons();
        _ = ApplySearchAsync();
    }

    private void OnShowDownloadedClicked(object sender, RoutedEventArgs e)
    {
        resultsQuickFilter = ResultsQuickFilter.Downloaded;
        OnlyInvoices = false;
        OnlyDownloaded = true;
        UpdateResultsFilterButtons();
        _ = ApplySearchAsync();
    }

    private void NavigateToResultsWithFilters(bool invoices = false, bool downloaded = false, bool missing = false, string? search = null)
    {
        PreviewTabVisibility = Visibility.Collapsed;
        resultsQuickFilter = downloaded ? ResultsQuickFilter.Downloaded : invoices ? ResultsQuickFilter.Invoices : ResultsQuickFilter.All;
        OnlyInvoices = invoices;
        OnlyDownloaded = downloaded;
        OnlyMissingFiles = missing;
        SearchText = search ?? string.Empty;
        SetCurrentPage(WorkspacePage.Results);
        UpdateResultsFilterButtons();
        _ = ApplySearchAsync();
    }

    private void OnDashboardInvoicesClicked(object sender, RoutedEventArgs e)
    {
        NavigateToResultsWithFilters(invoices: true);
    }

    private void OnDashboardAccountsClicked(object sender, RoutedEventArgs e)
    {
        SetCurrentPage(WorkspacePage.Accounts);
    }

    private void OnDashboardDocumentsClicked(object sender, RoutedEventArgs e)
    {
        NavigateToResultsWithFilters();
    }

    private void OnDashboardAttachmentsClicked(object sender, RoutedEventArgs e)
    {
        PreviewTabVisibility = Visibility.Collapsed;
        resultsQuickFilter = ResultsQuickFilter.All;
        OnlyInvoices = false;
        OnlyDownloaded = false;
        OnlyMissingFiles = false;
        OnlyWithAttachments = true;
        SetCurrentPage(WorkspacePage.Results);
        UpdateResultsFilterButtons();
        _ = ApplySearchAsync();
    }

    private void OnDashboardDownloadedClicked(object sender, RoutedEventArgs e)
    {
        NavigateToResultsWithFilters(downloaded: true);
    }

    private void OnDashboardMissingClicked(object sender, RoutedEventArgs e)
    {
        NavigateToResultsWithFilters(missing: true);
    }

    private void OnDashboardSearchClicked(object sender, RoutedEventArgs e)
    {
        NavigateToResultsWithFilters(search: DashboardSearchText?.Trim());
    }

    private void OnDashboardSearchKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            NavigateToResultsWithFilters(search: DashboardSearchText?.Trim());
        }
    }

    private async void OnDashboardRecentCandidateClicked(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is not CandidateListItem item)
        {
            return;
        }

        SetCurrentPage(WorkspacePage.Results);
        await ShowPreviewAsync(item.Candidate);
    }

    private void UpdateResultsFilterButtons()
    {
        if (AllResultsButton == null || InvoiceResultsButton == null || DownloadedResultsButton == null)
        {
            return;
        }

        AllResultsButton.Style = (Style)FindResource(resultsQuickFilter == ResultsQuickFilter.All ? "ToolPrimaryButton" : "ToolButton");
        InvoiceResultsButton.Style = (Style)FindResource(resultsQuickFilter == ResultsQuickFilter.Invoices ? "ToolPrimaryButton" : "ToolButton");
        DownloadedResultsButton.Style = (Style)FindResource(resultsQuickFilter == ResultsQuickFilter.Downloaded ? "ToolPrimaryButton" : "ToolButton");
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

        OnPropertyChanged(nameof(DashboardRecentCandidates));
        OnPropertyChanged(nameof(DashboardDocumentCountLabel));
        OnPropertyChanged(nameof(DashboardInvoiceCountLabel));
        OnPropertyChanged(nameof(DashboardMissingCountLabel));
        
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
        if (FindName("DashboardButton") is System.Windows.Controls.Button dashboardButton)
        {
            dashboardButton.IsEnabled = !isBusy;
        }
        ScanStartButton.IsEnabled = !isBusy;
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
            StopScanVisibility = Visibility.Visible;
            ScanPhaseLabel = "Initialisierung";
            ScanProgressBrush = (System.Windows.Media.Brush)FindResource("BlueBrush");
            LiveScanInfo = "Starte Scan...";
        }
        else
        {
            StopScanVisibility = Visibility.Collapsed;
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
            StopLiveUpdateTimer();
        }
        
        // Log state changes
        scanLogger.LogInfo($"[STATE] Busy={isBusy}, BusyVisibility={BusyVisibility}, StopButton should be {(isBusy ? "VISIBLE" : "HIDDEN")}");
    }

    private void StopLiveUpdateTimer()
    {
        if (liveUpdateTimer != null)
        {
            liveUpdateTimer.Stop();
            liveUpdateTimer = null;
            scanLogger.LogInfo("[TIMER] Live-Update Timer gestoppt");
        }
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
        ScannedMailCountSummary = $"{progress.AccountName}: {progress.MessagesScanned} Mails gescannt";
        
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
                _ = Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Render, () => {
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
                return "Keine Konten konfiguriert";
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
                var fileTypes = new List<string>();
                if (account.SearchPdf) fileTypes.Add("PDF");
                if (account.SearchDoc) fileTypes.Add("DOC");
                if (account.SearchDocx) fileTypes.Add("DOCX");
                if (account.SearchXls) fileTypes.Add("XLS");
                if (account.SearchXlsx) fileTypes.Add("XLSX");
                if (account.SearchPpt) fileTypes.Add("PPT");
                if (account.SearchPptx) fileTypes.Add("PPTX");
                if (account.SearchImages) fileTypes.Add("Bilder");
                if (account.SearchTxt) fileTypes.Add("TXT");
                if (account.SearchOther) fileTypes.Add("Sonstige");

                var excludedText = account.ExcludedFolderPatterns?.Count > 0
                    ? $", nicht gescannte Ordner: {string.Join(", ", account.ExcludedFolderPatterns.Take(3))}{(account.ExcludedFolderPatterns.Count > 3 ? " ..." : string.Empty)}"
                    : string.Empty;
                var ignoredNamesText = account.IgnoredAttachmentNamePatterns?.Count > 0
                    ? $", ignorierte Dateinamen: {string.Join(", ", account.IgnoredAttachmentNamePatterns.Take(3))}{(account.IgnoredAttachmentNamePatterns.Count > 3 ? " ..." : string.Empty)}"
                    : string.Empty;
                var fileTypeText = fileTypes.Count > 0 ? string.Join("/", fileTypes) : "keine Typen";

                accountInfos.Add($"{account.DisplayName} ({account.ImapHost}/{account.FolderName}) - Scan: {daysText}, gescannt: {fileTypeText}{excludedText}{ignoredNamesText}");
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
            StatusMessage = $"Protokoll gespeichert: {scanLogger.GetLogFilePath()}";
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
        OnPropertyChanged(nameof(CompactLogText));
        StatusMessage = "Protokoll gelöscht!";
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
