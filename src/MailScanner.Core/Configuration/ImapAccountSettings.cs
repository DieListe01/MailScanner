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
}
