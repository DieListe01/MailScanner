using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using MailScanner.App.Models;
using MailScanner.App.Services;
using MailScanner.Core.Configuration;
using WinForms = System.Windows.Forms;

namespace MailScanner.App;

public partial class MainWindow
{
    private EditableMailAccount? selectedEditorAccount;
    private string settingsStorageSummary = string.Empty;
    private string settingsProviderSummary = string.Empty;
    private int editorInitialLookbackDays = 30;
    private string editorExcludedFolderPatternsText = string.Empty;
    private string editorDatabasePath = string.Empty;
    private string editorDocumentRootPath = string.Empty;
    private bool isAccountEditorBusy;
    private string selectedEditorAccountSummary = string.Empty;

    public ObservableCollection<EditableMailAccount> EditorAccounts { get; } = [];

    public EditableMailAccount? SelectedEditorAccount
    {
        get => selectedEditorAccount;
        set
        {
            selectedEditorAccount = value;
            OnPropertyChanged();

            if (EmbeddedPasswordBox != null)
            {
                EmbeddedPasswordBox.Password = selectedEditorAccount?.Password ?? string.Empty;
            }

            OnPropertyChanged(nameof(CanSaveAccountSettings));
        }
    }

    public string SettingsStorageSummary
    {
        get => settingsStorageSummary;
        set
        {
            settingsStorageSummary = value;
            OnPropertyChanged();
        }
    }

    public string SettingsProviderSummary
    {
        get => settingsProviderSummary;
        set
        {
            settingsProviderSummary = value;
            OnPropertyChanged();
        }
    }

    public int EditorInitialLookbackDays
    {
        get => editorInitialLookbackDays;
        set
        {
            editorInitialLookbackDays = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(CanSaveAccountSettings));
        }
    }

    public string EditorExcludedFolderPatternsText
    {
        get => editorExcludedFolderPatternsText;
        set
        {
            editorExcludedFolderPatternsText = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(CanSaveAccountSettings));
        }
    }

    public string EditorDatabasePath
    {
        get => editorDatabasePath;
        set
        {
            editorDatabasePath = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(CanSaveAccountSettings));
        }
    }

    public string EditorDocumentRootPath
    {
        get => editorDocumentRootPath;
        set
        {
            editorDocumentRootPath = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(CanSaveAccountSettings));
        }
    }

    public bool CanSaveAccountSettings => !isAccountEditorBusy && EditorAccounts.Any(a => !string.IsNullOrWhiteSpace(a.EmailAddress));

    public string SelectedEditorAccountSummary
    {
        get => selectedEditorAccountSummary;
        set
        {
            selectedEditorAccountSummary = value;
            OnPropertyChanged();
        }
    }

    private void InitializeAccountEditor()
    {
        SettingsStorageSummary = AppDataPaths.GetUserSettingsFilePath();
        SettingsProviderSummary = settingsProvider is RegistryAppSettingsStore registryStore
            ? $"Geladen aus: {registryStore.GetLoadSourceSummary()}"
            : "Geladen ueber konfigurierten Einstellungsanbieter";
        LoadAccountEditorSettings();
    }

    private void LoadAccountEditorSettings()
    {
        try
        {
            var currentSettings = settingsProvider.GetCurrentSettings();
            if (settingsProvider is RegistryAppSettingsStore registryStore)
            {
                SettingsProviderSummary = $"Geladen aus: {registryStore.GetLoadSourceSummary()}";
            }
            EditorAccounts.Clear();

            EditorInitialLookbackDays = currentSettings.MailImport.InitialLookbackDays;
            EditorExcludedFolderPatternsText = string.Join(Environment.NewLine, currentSettings.MailImport.ExcludedFolderPatterns);
            EditorDatabasePath = currentSettings.Storage.DatabasePath;
            EditorDocumentRootPath = currentSettings.Storage.DocumentRootPath;

            foreach (var account in currentSettings.MailImport.Accounts)
            {
                EditorAccounts.Add(EditableMailAccount.FromSettings(account));
            }

            if (EditorAccounts.Count == 0)
            {
                EditorAccounts.Add(new EditableMailAccount());
            }

            SelectedEditorAccount = EditorAccounts[0];
            SyncSelectedEditorAccount();
            Dispatcher.InvokeAsync(() =>
            {
                if (EmbeddedAccountsListBox != null)
                {
                    EmbeddedAccountsListBox.SelectedItem = SelectedEditorAccount;
                }
            });
            DebugLogService.Instance.LogSettings($"Konten geladen: {EditorAccounts.Count}");
        }
        catch (Exception ex)
        {
            StatusMessage = $"Fehler beim Laden der Kontoeinstellungen: {ex.Message}";
            DebugLogService.Instance.LogError(StatusMessage);
        }
    }

    private void OnEmbeddedAddAccountClicked(object sender, RoutedEventArgs e)
    {
        var emptyAccount = EditorAccounts.FirstOrDefault(a => a.DisplayName == "Neues Konto"
            && string.IsNullOrWhiteSpace(a.EmailAddress)
            && string.IsNullOrWhiteSpace(a.ImapHost));

        if (emptyAccount != null)
        {
            SelectedEditorAccount = emptyAccount;
            SyncSelectedEditorAccount();
            return;
        }

        var newAccount = new EditableMailAccount
        {
            DisplayName = "Neues Konto",
            ImapPort = 993,
            UseSsl = true,
            FolderName = "INBOX"
        };

        EditorAccounts.Add(newAccount);
        SelectedEditorAccount = newAccount;
        SyncSelectedEditorAccount();
        StatusMessage = "Neues Konto vorbereitet.";
        OnPropertyChanged(nameof(CanSaveAccountSettings));
    }

    private void OnEmbeddedRemoveAccountClicked(object sender, RoutedEventArgs e)
    {
        if (SelectedEditorAccount is null)
        {
            return;
        }

        var index = EditorAccounts.IndexOf(SelectedEditorAccount);
        EditorAccounts.Remove(SelectedEditorAccount);

        if (EditorAccounts.Count == 0)
        {
            EditorAccounts.Add(new EditableMailAccount());
        }

        SelectedEditorAccount = EditorAccounts[Math.Min(index, EditorAccounts.Count - 1)];
        SyncSelectedEditorAccount();
        StatusMessage = "Konto entfernt.";
        OnPropertyChanged(nameof(CanSaveAccountSettings));
    }

    private void OnEmbeddedAccountSelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        SyncSelectedEditorAccount();
    }

    private void SyncSelectedEditorAccount()
    {
        if (EmbeddedAccountsListBox?.SelectedItem is EditableMailAccount account)
        {
            if (!ReferenceEquals(SelectedEditorAccount, account))
            {
                SelectedEditorAccount = account;
                return;
            }
        }

        if (EmbeddedPasswordBox != null)
        {
            EmbeddedPasswordBox.Password = SelectedEditorAccount?.Password ?? string.Empty;
        }

        SelectedEditorAccountSummary = SelectedEditorAccount is null
            ? "Kein Konto ausgewaehlt."
            : $"Aktives Scan-Setup: {SelectedEditorAccount.DisplayName} | {SelectedEditorAccount.EmailAddress} | {SelectedEditorAccount.ImapHost}:{SelectedEditorAccount.ImapPort} | Ordner {SelectedEditorAccount.FolderName}";
    }

    private void OnEmbeddedPasswordChanged(object sender, RoutedEventArgs e)
    {
        if (SelectedEditorAccount is not null && sender is System.Windows.Controls.PasswordBox passwordBox)
        {
            SelectedEditorAccount.Password = passwordBox.Password;
        }

        OnPropertyChanged(nameof(CanSaveAccountSettings));
    }

    private async void OnEmbeddedSaveClicked(object sender, RoutedEventArgs e)
    {
        if (!CanSaveAccountSettings)
        {
            StatusMessage = "Bitte zuerst ein vollstaendiges Konto erfassen.";
            return;
        }

        isAccountEditorBusy = true;
        OnPropertyChanged(nameof(CanSaveAccountSettings));

        try
        {
            await SaveEmbeddedSettingsAsync();
            RefreshExcludedFolderSummary();
            RefreshLookbackSummary();
            RefreshAccountSummary();
            StatusMessage = "Kontoeinstellungen gespeichert.";
            DebugLogService.Instance.LogSettings("Kontoeinstellungen gespeichert.");
        }
        catch (Exception ex)
        {
            StatusMessage = $"Fehler beim Speichern: {ex.Message}";
            DebugLogService.Instance.LogError(StatusMessage);
        }
        finally
        {
            isAccountEditorBusy = false;
            OnPropertyChanged(nameof(CanSaveAccountSettings));
        }
    }

    private async void OnEmbeddedTestSelectedClicked(object sender, RoutedEventArgs e)
    {
        if (SelectedEditorAccount is null)
        {
            StatusMessage = "Bitte zuerst ein Konto waehlen.";
            return;
        }

        await SaveEmbeddedSettingsAsync();
        await RunEmbeddedConnectionTestsAsync(SelectedEditorAccount.EmailAddress);
    }

    private async void OnEmbeddedTestAllClicked(object sender, RoutedEventArgs e)
    {
        await SaveEmbeddedSettingsAsync();
        await RunEmbeddedConnectionTestsAsync(null);
    }

    private async Task RunEmbeddedConnectionTestsAsync(string? emailFilter)
    {
        try
        {
            SetBusyState(true, "Teste Kontoverbindungen...");
            var results = await mailConnectionTestService.TestConnectionsAsync();
            var filtered = (string.IsNullOrWhiteSpace(emailFilter)
                ? results
                : results.Where(x => x.EmailAddress.Equals(emailFilter, StringComparison.OrdinalIgnoreCase))).ToArray();

            if (filtered.Length == 0)
            {
                StatusMessage = "Kein passendes Konto fuer den Verbindungstest gefunden.";
                return;
            }

            LastConnectionTestSummary = string.Join(" | ", filtered.Select(x => x.Success
                ? $"{x.DisplayName}: OK"
                : $"{x.DisplayName}: FEHLER - {x.Message}"));

            var successCount = filtered.Count(x => x.Success);
            StatusMessage = filtered.Length == 1
                ? (filtered[0].Success ? "Verbindung erfolgreich getestet." : $"Verbindung fehlgeschlagen: {filtered[0].Message}")
                : $"{successCount}/{filtered.Length} Verbindungen erfolgreich getestet.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Verbindungstest fehlgeschlagen: {ex.Message}";
        }
        finally
        {
            SetBusyState(false);
        }
    }

    private async Task SaveEmbeddedSettingsAsync()
    {
        var settings = new AppSettings
        {
            Storage = new StorageSettings
            {
                DatabasePath = EditorDatabasePath,
                DocumentRootPath = EditorDocumentRootPath
            },
            MailImport = new MailImportSettings
            {
                InitialLookbackDays = EditorInitialLookbackDays,
                ExcludedFolderPatterns = EditorExcludedFolderPatternsText
                    .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(pattern => pattern.Trim())
                    .Where(pattern => !string.IsNullOrWhiteSpace(pattern))
                    .ToArray(),
                Accounts = EditorAccounts
                    .Where(a => !string.IsNullOrWhiteSpace(a.EmailAddress))
                    .Select(a => a.ToSettings())
                    .ToArray()
            }
        };

        await settingsProvider.SaveAsync(settings);
    }

    private void OnEmbeddedBrowseDatabaseClicked(object sender, RoutedEventArgs e)
    {
        using var dialog = new WinForms.SaveFileDialog();
        dialog.Filter = "SQLite-Datenbankdatei (*.db)|*.db|Alle Dateien (*.*)|*.*";
        dialog.Title = "SQLite-Datenbankdatei auswaehlen oder neu anlegen";
        dialog.DefaultExt = "db";
        dialog.AddExtension = true;
        dialog.OverwritePrompt = false;
        dialog.CheckPathExists = true;
        if (!string.IsNullOrWhiteSpace(EditorDatabasePath))
        {
            dialog.FileName = System.IO.Path.GetFileName(EditorDatabasePath);
            dialog.InitialDirectory = System.IO.Path.GetDirectoryName(EditorDatabasePath);
        }

        if (dialog.ShowDialog() == WinForms.DialogResult.OK)
        {
            EditorDatabasePath = dialog.FileName;
        }
    }

    private void OnEmbeddedBrowseDocumentClicked(object sender, RoutedEventArgs e)
    {
        using var dialog = new WinForms.FolderBrowserDialog();
        if (!string.IsNullOrWhiteSpace(EditorDocumentRootPath))
        {
            dialog.SelectedPath = EditorDocumentRootPath;
        }

        if (dialog.ShowDialog() == WinForms.DialogResult.OK)
        {
            EditorDocumentRootPath = dialog.SelectedPath;
        }
    }
}
