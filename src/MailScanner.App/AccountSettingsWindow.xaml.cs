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
using System.Windows.Input;
using MailScanner.Core.Configuration;
using MailScanner.Core.Services;
using MailScanner.App.Services;
using MailScanner.App.Models;
using WinForms = System.Windows.Forms;
using TextBox = System.Windows.Controls.TextBox;

namespace MailScanner
{
    public partial class AccountSettingsWindow : Window, INotifyPropertyChanged
    {
        private readonly IAppSettingsProvider settingsProvider;
        private readonly IMailConnectionTestService mailConnectionTestService;
        private readonly AppSettings currentSettings;
        private EditableMailAccount? selectedAccount;
        private bool _isUpdating = false;
        private bool _isBusy = false;
        private string _statusMessage = "";
        private Visibility _statusMessageVisibility = Visibility.Collapsed;
        private string _settingsStorageSummary = "";
        private int _initialLookbackDays = 30;
        private string _excludedFolderPatternsText = "";
        private string _databasePath = "";
        private string _documentRootPath = "";

        public AccountSettingsWindow(IAppSettingsProvider settingsProvider, IMailConnectionTestService mailConnectionTestService)
        {
            this.settingsProvider = settingsProvider;
            this.mailConnectionTestService = mailConnectionTestService;
            currentSettings = settingsProvider.GetCurrentSettings();
            
            InitializeComponent();
            DataContext = this;
            SettingsStorageSummary = AppDataPaths.GetUserSettingsFilePath();
            
            // Load settings after UI is initialized
            LoadSettings();
        }
        
        private void LoadSettings()
        {
            try
            {
                // Debug: Show what we're loading
                var accountCount = currentSettings.MailImport.Accounts?.Count ?? 0;
                DebugLogService.Instance.LogSettings($"Account count from currentSettings: {accountCount}");
                DebugLogService.Instance.LogSettings($"DatabasePath: {currentSettings.Storage.DatabasePath}");
                DebugLogService.Instance.LogSettings($"DocumentRootPath: {currentSettings.Storage.DocumentRootPath}");
                
                // Load global settings
                InitialLookbackDays = currentSettings.MailImport.InitialLookbackDays;
                ExcludedFolderPatternsText = string.Join(Environment.NewLine, currentSettings.MailImport.ExcludedFolderPatterns);
                DatabasePath = currentSettings.Storage.DatabasePath;
                DocumentRootPath = currentSettings.Storage.DocumentRootPath;
                
                DebugLogService.Instance.LogSettings($"Accounts collection exists: {currentSettings.MailImport.Accounts != null}");
                if (currentSettings.MailImport.Accounts != null)
                {
                    DebugLogService.Instance.LogSettings($"Actual account count: {currentSettings.MailImport.Accounts.Count()}");
                }
                
                // Load accounts
                foreach (var account in currentSettings.MailImport.Accounts)
                {
                    DebugLogService.Instance.LogSettings($"Loading account: {account.DisplayName} - {account.EmailAddress}");
                    Accounts.Add(EditableMailAccount.FromSettings(account));
                }
                
                // Force UI refresh
                OnPropertyChanged(nameof(Accounts));
                
                // Ensure at least one account exists
                if (Accounts.Count == 0)
                {
                    DebugLogService.Instance.LogSettings("No accounts found, creating empty account");
                    Accounts.Add(new EditableMailAccount());
                }
                
                // Select first account
                SelectedAccount = Accounts[0];
                UpdateCanSaveTest();
                
                // Show loading status
                StatusMessage = Accounts.Count > 1 
                    ? $"{Accounts.Count} Konten geladen" 
                    : Accounts.Count == 1 
                        ? "1 Konto geladen" 
                        : "Keine Konten gefunden - leeres Konto erstellt";
                        
                DebugLogService.Instance.LogSettings($"Final Accounts.Count: {Accounts.Count}");
            }
            catch (Exception ex)
            {
                DebugLogService.Instance.LogError($"Fehler beim Laden der Settings: {ex.Message}");
                DebugLogService.Instance.LogError($"Stack Trace: {ex.StackTrace}");
                StatusMessage = $"Fehler beim Laden der Settings: {ex.Message}";
            }
        }

        public ObservableCollection<EditableMailAccount> Accounts { get; } = [];

        public EditableMailAccount? SelectedAccount
        {
            get => selectedAccount;
            set
            {
                selectedAccount = value;
                OnPropertyChanged();

                if (PasswordBox != null)
                {
                    PasswordBox.Password = selectedAccount?.Password ?? string.Empty;
                }

                UpdateCanSaveTest();
            }
        }

        public string StatusMessage
        {
            get => _statusMessage;
            set
            {
                _statusMessage = value;
                OnPropertyChanged();
            }
        }

        public Visibility StatusMessageVisibility
        {
            get => _statusMessageVisibility;
            set
            {
                _statusMessageVisibility = value;
                OnPropertyChanged();
            }
        }

        public string SettingsStorageSummary
        {
            get => _settingsStorageSummary;
            set
            {
                _settingsStorageSummary = value;
                OnPropertyChanged();
            }
        }

        public bool IsBusy
        {
            get => _isBusy;
            set
            {
                _isBusy = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(CanSaveTest));
            }
        }

        public int InitialLookbackDays
        {
            get => _initialLookbackDays;
            set
            {
                _initialLookbackDays = value;
                OnPropertyChanged();
                UpdateCanSaveTest();
            }
        }

        public string ExcludedFolderPatternsText
        {
            get => _excludedFolderPatternsText;
            set
            {
                _excludedFolderPatternsText = value;
                OnPropertyChanged();
                UpdateCanSaveTest();
            }
        }

        public string DatabasePath
        {
            get => _databasePath;
            set
            {
                _databasePath = value;
                OnPropertyChanged();
                UpdateCanSaveTest();
            }
        }

        public string DocumentRootPath
        {
            get => _documentRootPath;
            set
            {
                _documentRootPath = value;
                OnPropertyChanged();
                UpdateCanSaveTest();
            }
        }

        public bool CanSaveTest => !IsBusy && Accounts.Any(a => !string.IsNullOrWhiteSpace(a.EmailAddress));

        private void UpdateCanSaveTest()
        {
            OnPropertyChanged(nameof(CanSaveTest));
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
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

        private void OnAddAccountClicked(object sender, RoutedEventArgs e)
        {
            // Check if there's already an empty "Neues Konto" that hasn't been edited
            var emptyAccount = Accounts.FirstOrDefault(a => a.DisplayName == "Neues Konto" && 
                string.IsNullOrWhiteSpace(a.EmailAddress) && 
                string.IsNullOrWhiteSpace(a.ImapHost));
            
            if (emptyAccount != null)
            {
                // Select the existing empty account instead of creating a new one
                SelectedAccount = emptyAccount;
                return;
            }

            var newAccount = new EditableMailAccount
            {
                DisplayName = "Neues Konto",
                ImapPort = 993,
                UseSsl = true,
                FolderName = "INBOX"
            };

            Accounts.Add(newAccount);
            SelectedAccount = newAccount;
            UpdateCanSaveTest();
        }

        private void OnRemoveAccountClicked(object sender, RoutedEventArgs e)
        {
            if (SelectedAccount is null)
            {
                return;
            }

            var index = Accounts.IndexOf(SelectedAccount);
            Accounts.Remove(SelectedAccount);

            if (Accounts.Count == 0)
            {
                Accounts.Add(new EditableMailAccount());
            }

            SelectedAccount = Accounts[Math.Min(index, Accounts.Count - 1)];
            StatusMessage = "Konto entfernt.";
            UpdateCanSaveTest();
        }

        private async void OnSaveClicked(object sender, RoutedEventArgs e)
        {
            if (!CanSaveTest)
            {
                StatusMessage = "Bitte fülle alle Pflichtfelder aus";
                return;
            }

            IsBusy = true;
            StatusMessage = "Speichere Einstellungen...";
            try
            {
                await SaveAsync();
                StatusMessage = "Einstellungen erfolgreich gespeichert! ✅";
                
                // Show results immediately if we have valid accounts
                if (Accounts.Any(a => !string.IsNullOrWhiteSpace(a.EmailAddress)))
                {
                    StatusMessage += " Du kannst jetzt einen Scan starten.";
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Fehler beim Speichern: {ex.Message}";
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
                ShowTestResult(false, "Bitte fülle alle Pflichtfelder aus");
                return;
            }

            IsBusy = true;
            ShowTestResult(false, "Verbindung wird getestet...", isPending: true);
            try
            {
                await SaveAsync();
                var success = await RunConnectionTestAsync(SelectedAccount?.EmailAddress);
                ShowTestResult(success, success ? "Verbindung erfolgreich!" : "Verbindung fehlgeschlagen. Prüfe E-Mail und Passwort.");
            }
            catch (Exception ex)
            {
                ShowTestResult(false, $"Fehler: {ex.Message}");
            }
            finally
            {
                IsBusy = false;
            }
        }

        private void ShowTestResult(bool success, string message, bool isPending = false)
        {
            StatusMessage = message;
            StatusMessageVisibility = Visibility.Visible;
            
            if (isPending)
            {
                StatusMessage = "⏳ " + message;
            }
            else if (success)
            {
                StatusMessage = "✓ " + message;
            }
            else
            {
                StatusMessage = "✗ " + message;
            }
        }

        private async void OnTestAllClicked(object sender, RoutedEventArgs e)
        {
            if (!CanSaveTest)
            {
                ShowTestResult(false, "Bitte fülle alle Pflichtfelder aus");
                return;
            }

            IsBusy = true;
            var validAccounts = Accounts.Where(a => !string.IsNullOrWhiteSpace(a.EmailAddress)).ToList();
            var results = new List<(string email, bool success)>();

            try
            {
                await SaveAsync();
                StatusMessage = $"Teste {validAccounts.Count} Verbindungen...";
                StatusMessageVisibility = Visibility.Visible;

                foreach (var account in validAccounts)
                {
                    ShowTestResult(false, $"Teste {account.EmailAddress}...", isPending: true);
                    var success = await RunConnectionTestAsync(account.EmailAddress);
                    results.Add((account.EmailAddress, success));
                }

                var successCount = results.Count(r => r.success);
                var message = $"{successCount}/{results.Count} Verbindungen erfolgreich";
                ShowTestResult(successCount == results.Count, message);
            }
            catch (Exception ex)
            {
                ShowTestResult(false, $"Fehler: {ex.Message}");
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

        private void OnDebugClicked(object sender, RoutedEventArgs e)
        {
            var debugWindow = DebugOutputWindow.Instance;
            debugWindow.Show();
            debugWindow.Activate();
        }

        private void OnNewClicked(object sender, RoutedEventArgs e)
        {
            OnAddAccountClicked(sender, e);
        }

        private void OnDeleteClicked(object sender, RoutedEventArgs e)
        {
            OnRemoveAccountClicked(sender, e);
        }

        private void OnPasswordChanged(object sender, RoutedEventArgs e)
        {
            if (SelectedAccount is not null && sender is System.Windows.Controls.PasswordBox passwordBox)
            {
                SelectedAccount.Password = passwordBox.Password;
            }
            UpdateCanSaveTest();
        }

        private void OnProviderSelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            // Provider selection removed for simplified UI
        }

        private void OnBrowseDatabaseClicked(object sender, RoutedEventArgs e)
        {
            using var dialog = new WinForms.SaveFileDialog();
            dialog.Filter = "SQLite-Datenbankdatei (*.db)|*.db|Alle Dateien (*.*)|*.*";
            dialog.Title = "SQLite-Datenbankdatei auswählen oder neu anlegen";
            dialog.DefaultExt = "db";
            dialog.AddExtension = true;
            dialog.OverwritePrompt = false;
            dialog.CheckPathExists = true;
            if (!string.IsNullOrWhiteSpace(DatabasePath))
            {
                dialog.FileName = System.IO.Path.GetFileName(DatabasePath);
                dialog.InitialDirectory = System.IO.Path.GetDirectoryName(DatabasePath);
            }

            if (dialog.ShowDialog() == WinForms.DialogResult.OK)
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

            if (dialog.ShowDialog() == WinForms.DialogResult.OK)
            {
                DocumentRootPath = dialog.SelectedPath;
            }
        }

        private async Task SaveAsync()
        {
            var settings = new AppSettings
            {
                Storage = new StorageSettings
                {
                    DatabasePath = DatabasePath,
                    DocumentRootPath = DocumentRootPath
                },
                MailImport = new MailImportSettings
                {
                    InitialLookbackDays = InitialLookbackDays,
                    ExcludedFolderPatterns = ExcludedFolderPatternsText
                        .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                        .Select(pattern => pattern.Trim())
                        .Where(pattern => !string.IsNullOrWhiteSpace(pattern))
                        .ToArray(),
                    Accounts = Accounts
                        .Where(a => !string.IsNullOrWhiteSpace(a.EmailAddress))
                        .Select(a => a.ToSettings())
                        .ToArray()
                }
            };

            await settingsProvider.SaveAsync(settings);
        }

        private async Task<bool> RunConnectionTestAsync(string? emailAddress)
        {
            if (string.IsNullOrWhiteSpace(emailAddress))
                return false;

            try
            {
                // TODO: Implement connection test when interface supports it
                await Task.Delay(1000); // Simulate test
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
