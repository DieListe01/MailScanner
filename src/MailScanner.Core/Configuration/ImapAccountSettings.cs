namespace MailScanner.Core.Configuration;

public sealed class ImapAccountSettings
{
    public string ProviderName { get; init; } = "Benutzerdefiniert";
    public string DisplayName { get; init; } = string.Empty;
    public string EmailAddress { get; init; } = string.Empty;
    public string UserName { get; init; } = string.Empty;
    public string Password { get; init; } = string.Empty;
    public string ImapHost { get; init; } = string.Empty;
    public int ImapPort { get; init; } = 993;
    public bool UseSsl { get; init; } = true;
    public string FolderName { get; init; } = "INBOX";
    public IReadOnlyCollection<string> ExcludedFolderPatterns { get; init; } = [];
    
    // Dateityp-Filter pro Konto
    public bool SearchPdf { get; init; } = true;
    public bool SearchDoc { get; init; } = true;
    public bool SearchDocx { get; init; } = true;
    public bool SearchXlsx { get; init; } = false;
    public bool SearchXls { get; init; } = false;
    public bool SearchPptx { get; init; } = false;
    public bool SearchPpt { get; init; } = false;
    public bool SearchImages { get; init; } = false;
    public bool SearchTxt { get; init; } = false;
    public bool SearchOther { get; init; } = false;
}
