namespace MailScanner.Core.Configuration;

public sealed class MailImportSettings
{
    public int InitialLookbackDays { get; init; } = 0;
    public IReadOnlyCollection<string> ExcludedFolderPatterns { get; init; } = [];
    public IReadOnlyCollection<ImapAccountSettings> Accounts { get; init; } = [];
}
