namespace MailScanner.Core.Models;

public sealed class MailImportProgress
{
    public string AccountName { get; init; } = string.Empty;
    public string FolderName { get; init; } = string.Empty;
    public int AccountsTotal { get; init; }
    public int AccountsCompleted { get; init; }
    public int FoldersTotal { get; init; }
    public int FoldersCompleted { get; init; }
    public int ConfiguredLookbackDays { get; init; }
    public bool IsFullScan { get; init; }
    public int MessagesScanned { get; init; }
    public int AttachmentMessagesFound { get; init; }
    public int PdfCandidatesFound { get; init; }
    public int InvoiceMatchesFound { get; init; }
    public int? OldestScannedMessageAgeDays { get; init; }
    public DateTimeOffset? OldestScannedMessageDate { get; init; }
    public int ExcludedFolderCount { get; init; }
    public string StatusText { get; init; } = string.Empty;
}
