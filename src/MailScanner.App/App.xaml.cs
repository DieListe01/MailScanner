using System.IO;
using System.Windows;
using MailScanner.App.Services;
using MailScanner.Core.Configuration;
using MailScanner.Core.Services;
using MailScanner.Data;
using MailScanner.Data.Repositories;
using MailScanner.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;

namespace MailScanner.App;

public partial class App : Application
{
    private MailScannerDbContext? dbContext;
    private JsonAppSettingsStore? settingsStore;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var settingsFilePath = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
        settingsStore = new JsonAppSettingsStore(settingsFilePath);
        var settings = settingsStore.GetCurrentSettings();

        Directory.CreateDirectory(Path.GetDirectoryName(settings.Storage.DatabasePath) ?? AppContext.BaseDirectory);
        Directory.CreateDirectory(settings.Storage.DocumentRootPath);

        var dbOptions = new DbContextOptionsBuilder<MailScannerDbContext>()
            .UseSqlite($"Data Source={settings.Storage.DatabasePath}")
            .Options;

        dbContext = new MailScannerDbContext(dbOptions);
        await DatabaseInitializer.InitializeAsync(dbContext);

        IAppSettingsProvider settingsProvider = settingsStore;
        IMailboxScanStateStore mailboxScanStateStore = new MailboxScanStateStore(dbContext);
        IDocumentCandidateStore documentCandidateStore = new DocumentCandidateStore(dbContext);
        IDocumentRecordStore documentRecordStore = new DocumentRecordStore(dbContext);
        IMailImportService mailImportService = new ImapMailImportService(settingsProvider, mailboxScanStateStore, documentCandidateStore);
        IMailConnectionTestService mailConnectionTestService = new ImapConnectionTestService(settingsProvider);
        IDocumentDownloadService documentDownloadService = new ImapDocumentDownloadService(settingsProvider, documentCandidateStore, documentRecordStore);
        var appVersionService = new AppVersionService();
        var releaseUpdateService = new GitHubReleaseUpdateService("DieListe01", "MailScanner");

        var mainWindow = new MainWindow(
            settingsProvider,
            mailImportService,
            mailConnectionTestService,
            documentCandidateStore,
            documentDownloadService,
            appVersionService,
            releaseUpdateService);
        MainWindow = mainWindow;
        mainWindow.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        dbContext?.Dispose();
        base.OnExit(e);
    }
}

