using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using MailScanner.App.Models;
using MailScanner.App.Services;
using MailScanner.Core.Configuration;
using MailScanner.Core.Models;
using MailScanner.Core.Services;

namespace MailScanner.App;

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
        }
    }

    public string ExcludedFolderPatternsText
    {
        get => excludedFolderPatternsText;
        set
        {
            excludedFolderPatternsText = value;
            OnPropertyChanged();
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

        foreach (var account in currentSettings.MailImport.Accounts)
        {
            Accounts.Add(EditableMailAccount.FromSettings(account));
        }

        if (Accounts.Count == 0)
        {
            Accounts.Add(new EditableMailAccount());
        }

        SelectedAccount = Accounts[0];
    }

    private void OnAddAccountClicked(object sender, RoutedEventArgs e)
    {
        var account = new EditableMailAccount();
        ApplyProviderPreset(account, account.ProviderName, overwriteUserEntries: true);
        Accounts.Add(account);
        SelectedAccount = account;
        StatusMessage = "Neues Konto angelegt.";
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
    }

    private async void OnSaveClicked(object sender, RoutedEventArgs e)
    {
        await SaveAsync();
    }

    private async void OnTestSelectedClicked(object sender, RoutedEventArgs e)
    {
        if (SelectedAccount is null)
        {
            return;
        }

        await SaveAsync();
        await RunConnectionTestAsync(SelectedAccount.EmailAddress);
    }

    private async void OnTestAllClicked(object sender, RoutedEventArgs e)
    {
        await SaveAsync();
        await RunConnectionTestAsync();
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
    }

    private void OnProviderSelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (SelectedAccount is null)
        {
            return;
        }

        ApplyProviderPreset(SelectedAccount, SelectedAccount.ProviderName, overwriteUserEntries: false);
    }

    private async Task SaveAsync()
    {
        var updatedSettings = new AppSettings
        {
            Storage = currentSettings.Storage,
            MailImport = new MailImportSettings
            {
                InitialLookbackDays = InitialLookbackDays,
                ExcludedFolderPatterns = ParseExcludedFolderPatterns(ExcludedFolderPatternsText),
                Accounts = Accounts.Select(x => x.ToSettings()).ToArray()
            }
        };

        await settingsProvider.SaveAsync(updatedSettings);
        StatusMessage = $"Einstellungen gespeichert in {AppDataPaths.GetUserSettingsFilePath()}.";
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
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
