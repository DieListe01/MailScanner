using System.ComponentModel;
using System.Runtime.CompilerServices;
using MailScanner.Core.Configuration;

namespace MailScanner.App.Models;

public sealed class EditableMailAccount : INotifyPropertyChanged
{
    private string providerName = "Gmail";
    private string displayName = "Neues Konto";
    private string emailAddress = string.Empty;
    private string userName = string.Empty;
    private string password = string.Empty;
    private string imapHost = string.Empty;
    private int imapPort = 993;
    private bool useSsl = true;
    private string folderName = "INBOX";
    private string excludedFolderPatternsText = string.Empty;

    public string ProviderName { get => providerName; set => SetField(ref providerName, value); }
    public string DisplayName { get => displayName; set => SetField(ref displayName, value); }
    public string EmailAddress { get => emailAddress; set => SetField(ref emailAddress, value); }
    public string UserName { get => userName; set => SetField(ref userName, value); }
    public string Password { get => password; set => SetField(ref password, value); }
    public string ImapHost { get => imapHost; set => SetField(ref imapHost, value); }
    public int ImapPort { get => imapPort; set => SetField(ref imapPort, value); }
    public bool UseSsl { get => useSsl; set => SetField(ref useSsl, value); }
    public string FolderName { get => folderName; set => SetField(ref folderName, value); }
    public string ExcludedFolderPatternsText { get => excludedFolderPatternsText; set => SetField(ref excludedFolderPatternsText, value); }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ImapAccountSettings ToSettings()
    {
        return new ImapAccountSettings
        {
            ProviderName = ProviderName,
            DisplayName = DisplayName,
            EmailAddress = EmailAddress,
            UserName = UserName,
            Password = Password,
            ImapHost = ImapHost,
            ImapPort = ImapPort,
            UseSsl = UseSsl,
            FolderName = FolderName,
            ExcludedFolderPatterns = ParseExcludedFolderPatterns(ExcludedFolderPatternsText)
        };
    }

    public static EditableMailAccount FromSettings(ImapAccountSettings settings)
    {
        return new EditableMailAccount
        {
            ProviderName = string.IsNullOrWhiteSpace(settings.ProviderName) ? "Benutzerdefiniert" : settings.ProviderName,
            DisplayName = settings.DisplayName,
            EmailAddress = settings.EmailAddress,
            UserName = settings.UserName,
            Password = settings.Password,
            ImapHost = settings.ImapHost,
            ImapPort = settings.ImapPort,
            UseSsl = settings.UseSsl,
            FolderName = settings.FolderName,
            ExcludedFolderPatternsText = string.Join(Environment.NewLine, settings.ExcludedFolderPatterns)
        };
    }

    private static string[] ParseExcludedFolderPatterns(string input)
    {
        return input
            .Split(['\r', '\n', ';', ','], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private void SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return;
        }

        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
