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
    private int initialLookbackDays = 0;
    
    // Dateityp-Filter
    private bool searchPdf = true;
    private bool searchDoc = true;
    private bool searchDocx = true;
    private bool searchXlsx = false;
    private bool searchXls = false;
    private bool searchPptx = false;
    private bool searchPpt = false;
    private bool searchImages = false;
    private bool searchTxt = false;
    private bool searchOther = false;

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
    public int InitialLookbackDays { get => initialLookbackDays; set => SetField(ref initialLookbackDays, value); }
    
    // Dateityp-Filter Properties
    public bool SearchPdf { get => searchPdf; set => SetField(ref searchPdf, value); }
    public bool SearchDoc { get => searchDoc; set => SetField(ref searchDoc, value); }
    public bool SearchDocx { get => searchDocx; set => SetField(ref searchDocx, value); }
    public bool SearchXlsx { get => searchXlsx; set => SetField(ref searchXlsx, value); }
    public bool SearchXls { get => searchXls; set => SetField(ref searchXls, value); }
    public bool SearchPptx { get => searchPptx; set => SetField(ref searchPptx, value); }
    public bool SearchPpt { get => searchPpt; set => SetField(ref searchPpt, value); }
    public bool SearchImages { get => searchImages; set => SetField(ref searchImages, value); }
    public bool SearchTxt { get => searchTxt; set => SetField(ref searchTxt, value); }
    public bool SearchOther { get => searchOther; set => SetField(ref searchOther, value); }

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
            ExcludedFolderPatterns = ParseExcludedFolderPatterns(ExcludedFolderPatternsText),
            SearchPdf = SearchPdf,
            SearchDoc = SearchDoc,
            SearchDocx = SearchDocx,
            SearchXlsx = SearchXlsx,
            SearchXls = SearchXls,
            SearchPptx = SearchPptx,
            SearchPpt = SearchPpt,
            SearchImages = SearchImages,
            SearchTxt = SearchTxt,
            SearchOther = SearchOther
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
            ExcludedFolderPatternsText = string.Join(Environment.NewLine, settings.ExcludedFolderPatterns),
            SearchPdf = settings.SearchPdf,
            SearchDoc = settings.SearchDoc,
            SearchDocx = settings.SearchDocx,
            SearchXlsx = settings.SearchXlsx,
            SearchXls = settings.SearchXls,
            SearchPptx = settings.SearchPptx,
            SearchPpt = settings.SearchPpt,
            SearchImages = settings.SearchImages,
            SearchTxt = settings.SearchTxt,
            SearchOther = settings.SearchOther
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
