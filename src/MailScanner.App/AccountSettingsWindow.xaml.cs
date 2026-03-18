using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Net.Mail;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using MailScanner.App.Controls;
using MailScanner.App.Models;
using MailScanner.App.Services;
using MailScanner.Core.Configuration;
using MailScanner.Core.Models;
using MailScanner.Core.Services;
using WinForms = System.Windows.Forms;

namespace MailScanner.App
{
    public partial class AccountSettingsWindow : Window, INotifyPropertyChanged
    {
        private readonly IAppSettingsProvider settingsProvider;
        private readonly IMailConnectionTestService mailConnectionTestService;
        private readonly AppSettings currentSettings;
        private EditableMailAccount? selectedAccount;
        private string selectedProviderHint = MailProviderCatalog.GetByName("Gmail").Hint;
        private string statusMessage = "Bearbeite deine Konten und speichere die Einstellungen.";
        private string settingsStorageSummary = AppDataPaths.GetUserSettingsFilePath();
        private string testResultSummary = string.Empty;
        private string excludedFolderPatternsText = string.Empty;
        private int initialLookbackDays;
        private string databasePath = string.Empty;
        private string documentRootPath = string.Empty;
        private string _validationMessage;
        public string ValidationMessage
        {
            get => _validationMessage;
            set { _validationMessage = value; OnPropertyChanged(); OnPropertyChanged(nameof(ValidationBannerState)); }
        }

        public BannerState ValidationBannerState => string.IsNullOrEmpty(ValidationMessage) ? BannerState.None : BannerState.Error;
        private bool _isBusy;
        public bool IsBusy
        {
            get => _isBusy;
            set { _isBusy = value; OnPropertyChanged(); UpdateCanSaveTest(); }
        }
        private bool _canSaveTest;
        public bool CanSaveTest
        {
            get => _canSaveTest;
            private set { _canSaveTest = value; OnPropertyChanged(); }
        }

        public ObservableCollection<EditableMailAccount> Accounts { get; } = [];
        public IReadOnlyList<string> ProviderNames { get; } = MailProviderCatalog.All.Select(x => x.Name).ToArray();

        public EditableMailAccount? SelectedAccount
        {
            get => selectedAccount;
            set
            {
                selectedAccount = value;
                OnPropertyChanged();

                if (PasswordInput is not null)
                {
                    PasswordInput.Password = selectedAccount?.Password ?? string.Empty;
                }

                SelectedProviderHint = MailProviderCatalog.GetByName(selectedAccount?.ProviderName).Hint;
                UpdateCanSaveTest();
            }
        }

        public string SelectedProviderHint
        {
            get => selectedProviderHint;
            set
            {
                selectedProviderHint = value;
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

        public string SettingsStorageSummary
        {
            get => settingsStorageSummary;
            set
            {
                settingsStorageSummary = value;
                OnPropertyChanged();
            }
        }

        public string TestResultSummary
        {
            get => testResultSummary;
            set
            {
                testResultSummary = value;
                OnPropertyChanged();
            }
        }

        public int InitialLookbackDays
        {
            get => initialLookbackDays;
            set
            {
                initialLookbackDays = value;
                OnPropertyChanged();
                UpdateCanSaveTest();
            }
        }

        public string ExcludedFolderPatternsText
        {
            get => excludedFolderPatternsText;
            set
            {
                excludedFolderPatternsText = value;
                OnPropertyChanged();
                UpdateCanSaveTest();
            }
        }

        public string DatabasePath
        {
            get => databasePath;
            set
            {
                databasePath = value;
                OnPropertyChanged();
                UpdateCanSaveTest();
            }
        }

        public string DocumentRootPath
        {
            get => documentRootPath;
            set
            {
                documentRootPath = value;
                OnPropertyChanged();
                UpdateCanSaveTest();
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        public AccountSettingsWindow(IAppSettingsProvider settingsProvider, IMailConnectionTestService mailConnectionTestService)
        {
            this.settingsProvider = settingsProvider;
            this.mailConnectionTestService = mailConnectionTestService;
            currentSettings = settingsProvider.GetCurrentSettings();

            InitializeComponent();
            DataContext = this;
            SettingsStorageSummary = AppDataPaths.GetUserSettingsFilePath();
            InitialLookbackDays = currentSettings.MailImport.InitialLookbackDays;
            ExcludedFolderPatternsText = string.Join(Environment.NewLine, currentSettings.MailImport.ExcludedFolderPatterns);
            databasePath = currentSettings.Storage.DatabasePath;
            documentRootPath = currentSettings.Storage.DocumentRootPath;

            foreach (var account in currentSettings.MailImport.Accounts)
            {
                Accounts.Add(EditableMailAccount.FromSettings(account));
            }

            if (Accounts.Count == 0)
            {
                Accounts.Add(new EditableMailAccount());
            }

            SelectedAccount = Accounts[0];
            UpdateCanSaveTest();
        }

        private void OnAddAccountClicked(object sender, RoutedEventArgs e)
        {
            var account = new EditableMailAccount();
            ApplyProviderPreset(account, account.ProviderName, overwriteUserEntries: true);
            Accounts.Add(account);
            SelectedAccount = account;
            StatusMessage = "Neues Konto angelegt.";
            UpdateCanSaveTest();
        }

        private void OnRemoveAccountClicked(object sender, RoutedEventArgs e)
        {
            if (SelectedAccount is null)
            {
                return;
            }

            var toRemove = SelectedAccount;
            Accounts.Remove(toRemove);

            if (Accounts.Count == 0)
            {
                Accounts.Add(new EditableMailAccount());
            }

            SelectedAccount = Accounts[0];
            StatusMessage = "Konto entfernt.";
            UpdateCanSaveTest();
        }

        private async void OnSaveClicked(object sender, RoutedEventArgs e)
        {
            if (!CanSaveTest)
            {
                return;
            }

            IsBusy = true;
            try
            {
                await SaveAsync();
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async void OnTestSelectedClicked(object sender, RoutedEventArgs e)
        {
            if (!CanSaveTest)
            {
                return;
            }

            IsBusy = true;
            try
            {
                await SaveAsync();
                await RunConnectionTestAsync(SelectedAccount.EmailAddress);
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async void OnTestAllClicked(object sender, RoutedEventArgs e)
        {
            if (!CanSaveTest)
            {
                return;
            }

            IsBusy = true;
            try
            {
                await SaveAsync();
                await RunConnectionTestAsync();
            }
            finally
            {
                IsBusy = false;
            }
        }

        private void OnCloseClicked(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            Close();
        }

        private void OnPasswordChanged(object sender, RoutedEventArgs e)
        {
            if (SelectedAccount is not null)
            {
                SelectedAccount.Password = PasswordInput.Password;
            }
            UpdateCanSaveTest();
        }

        private void OnProviderSelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (SelectedAccount is null)
            {
                return;
            }

            ApplyProviderPreset(SelectedAccount, SelectedAccount.ProviderName, overwriteUserEntries: false);
            UpdateCanSaveTest();
        }

        private void OnBrowseDatabaseClicked(object sender, RoutedEventArgs e)
        {
            using var dialog = new WinForms.OpenFileDialog();
            dialog.Filter = "SQLite-Datenbankdatei (*.db)|*.db|Alle Dateien (*.*)|*.*";
            dialog.Title = "SQLite-Datenbankdatei auswählen";
            if (!string.IsNullOrWhiteSpace(DatabasePath))
            {
                dialog.FileName = System.IO.Path.GetFileName(DatabasePath);
                dialog.InitialDirectory = System.IO.Path.GetDirectoryName(DatabasePath);
            }
            if (dialog.ShowDialog(GetOwnerWindow()) == WinForms.DialogResult.OK)
            {
                DatabasePath = dialog.FileName;
            }
        }

        private void OnBrowseDocumentClicked(object sender, RoutedEventArgs e)
        {
            using var dialog = new WinForms.FolderBrowserDialog();
            if (!string.IsNullOrWhiteSpace(DocumentRootPath))
            {
                dialog.SelectedPath = DocumentRootPath;
            }
            if (dialog.ShowDialog(GetOwnerWindow()) == WinForms.DialogResult.OK)
            {
                DocumentRootPath = dialog.SelectedPath;
            }
        }

        private System.Windows.Forms.IWin32Window GetOwnerWindow()
        {
            return new WindowWrapper(new WindowInteropHelper(this).Handle);
        }

        private class WindowWrapper : System.Windows.Forms.IWin32Window
        {
            private readonly System.IntPtr _handle;
            public WindowWrapper(System.IntPtr handle) => _handle = handle;
            public System.IntPtr Handle => _handle;
        }

        private async Task SaveAsync()
        {
            var updatedSettings = new AppSettings
            {
                Storage = new StorageSettings
                {
                    DatabasePath = DatabasePath,
                    DocumentRootPath = DocumentRootPath
                },
                MailImport = new MailImportSettings
                {
                    InitialLookbackDays = InitialLookbackDays,
                    ExcludedFolderPatterns = ParseExcludedFolderPatterns(ExcludedFolderPatternsText),
                    Accounts = Accounts.Select(x => x.ToSettings()).ToArray()
                }
            };

            await settingsProvider.SaveAsync(updatedSettings);
            StatusMessage = $"Einstellungen gespeichert in {AppDataPaths.GetUserSettingsFilePath()}.";
            ValidationMessage = string.Empty; // clear validation on success
            UpdateCanSaveTest(); // re-evaluate (should stay true if not busy)
        }

        private async Task RunConnectionTestAsync(string? emailAddress = null)
        {
            StatusMessage = string.IsNullOrWhiteSpace(emailAddress)
                ? "Teste alle Konten..."
                : $"Teste Konto {emailAddress}...";

            var results = await mailConnectionTestService.TestConnectionsAsync();
            var filtered = string.IsNullOrWhiteSpace(emailAddress)
                ? results
                : results.Where(x => x.EmailAddress.Equals(emailAddress, StringComparison.OrdinalIgnoreCase)).ToArray();

            TestResultSummary = string.Join(Environment.NewLine, filtered.Select(FormatResult));
            StatusMessage = filtered.All(x => x.Success)
                ? "Verbindungstest erfolgreich."
                : "Mindestens ein Konto hat noch Probleme.";
            ValidationMessage = string.Empty; // clear validation on successful test
            UpdateCanSaveTest(); // re-evaluate
        }

        private static string FormatResult(MailAccountTestResult result)
        {
            return result.Success
                ? $"{result.DisplayName}: OK"
                : $"{result.DisplayName}: {result.Message}";
        }

        private static string[] ParseExcludedFolderPatterns(string input)
        {
            return input
                .Split(['\r', '\n', ';', ','], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        private void ApplyProviderPreset(EditableMailAccount account, string? providerName, bool overwriteUserEntries)
        {
            var preset = MailProviderCatalog.GetByName(providerName);

            account.ProviderName = preset.Name;
            account.ImapHost = preset.ImapHost;
            account.ImapPort = preset.ImapPort;
            account.UseSsl = preset.UseSsl;
            account.FolderName = preset.FolderName;

            if (overwriteUserEntries || string.IsNullOrWhiteSpace(account.UserName))
            {
                account.UserName = account.EmailAddress;
            }

            SelectedProviderHint = preset.Hint;
            StatusMessage = $"Provider-Preset '{preset.Name}' angewendet.";
            UpdateCanSaveTest();
        }

        private string ValidateSettings()
        {
            if (string.IsNullOrWhiteSpace(DatabasePath))
                return "Datenbank-Pfad darf nicht leer sein.";
            if (!DatabasePath.ToLowerInvariant().EndsWith(".db"))
                return "Datenbank-Pfad muss auf eine .db-Datei zeigen.";
            var dbDir = System.IO.Path.GetDirectoryName(DatabasePath);
            if (!System.IO.Directory.Exists(dbDir))
                return $"Verzeichnis für Datenbank existiert nicht: {dbDir}";
            // Optional: check if file exists or creatable

            if (string.IsNullOrWhiteSpace(DocumentRootPath))
                return "Dokumenten-Ordner darf nicht leer sein.";
            if (!System.IO.Directory.Exists(DocumentRootPath))
                return $"Dokumenten-Ordner existiert nicht: {DocumentRootPath}";

            if (Accounts == null || Accounts.Count == 0)
                return "Mindestens ein Konto muss vorhanden sein.";

            var emailSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var acc in Accounts)
            {
                if (acc == null) continue;
                if (string.IsNullOrWhiteSpace(acc.EmailAddress))
                    return "E-Mail-Adresse darf nicht leer sein.";
                if (!IsValidEmail(acc.EmailAddress))
                    return $"E-Mail-Adresse ungültig: {acc.EmailAddress}";
                if (!emailSet.Add(acc.EmailAddress))
                    return $"Doppelte E-Mail-Adresse: {acc.EmailAddress}";
                if (string.IsNullOrWhiteSpace(acc.ImapHost))
                    return $"IMAP-Host darf nicht leer sein bei Konto {acc.EmailAddress}";
                if (acc.ImapPort <= 0 || acc.ImapPort > 65535)
                    return $"IMAP-Port muss zwischen 1 und 65535 liegen bei Konto {acc.EmailAddress}";
            }

            if (InitialLookbackDays < 0)
                return "Rückblick beim Erstscan darf nicht negativ sein.";

            return string.Empty;
        }

        private static bool IsValidEmail(string email)
        {
            if (string.IsNullOrWhiteSpace(email))
                return false;
            try
            {
                var addr = new System.Net.Mail.MailAddress(email);
                return addr.Address == email;
            }
            catch
            {
                return false;
            }
        }

        private void UpdateCanSaveTest()
        {
            var validation = ValidateSettings();
            ValidationMessage = validation;
            CanSaveTest = !IsBusy && string.IsNullOrEmpty(validation);
        }

        private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}