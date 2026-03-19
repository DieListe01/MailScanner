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
    private int editorInitialLookbackDays = 30;
    private string editorExcludedFolderPatternsText = string.Empty;
    private string editorDatabasePath = string.Empty;
    private string editorDocumentRootPath = string.Empty;
    private bool isAccountEditorBusy;

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

    private void InitializeAccountEditor()
    {
        SettingsStorageSummary = AppDataPaths.GetUserSettingsFilePath();
        LoadAccountEditorSettings();
    }

    private void LoadAccountEditorSettings()
    {
        try
        {
            var currentSettings = settingsProvider.GetCurrentSettings();
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
        StatusMessage = "Konto entfernt.";
        OnPropertyChanged(nameof(CanSaveAccountSettings));
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
        using var dialog = new WinForms.OpenFileDialog();
        dialog.Filter = "SQLite-Datenbankdatei (*.db)|*.db|Alle Dateien (*.*)|*.*";
        dialog.Title = "SQLite-Datenbankdatei auswaehlen";
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
