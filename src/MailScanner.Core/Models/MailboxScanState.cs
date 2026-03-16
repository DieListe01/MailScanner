namespace MailScanner.Core.Models;

public sealed class MailboxScanState
{
    public string AccountAddress { get; init; } = string.Empty;
    public string FolderName { get; init; } = string.Empty;
    public uint LastScannedUid { get; init; }
    public DateTimeOffset UpdatedAt { get; init; } = DateTimeOffset.UtcNow;
}
