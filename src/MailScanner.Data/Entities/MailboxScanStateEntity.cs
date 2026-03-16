namespace MailScanner.Data.Entities;

public sealed class MailboxScanStateEntity
{
    public int Id { get; set; }
    public string AccountAddress { get; set; } = string.Empty;
    public string FolderName { get; set; } = string.Empty;
    public uint LastScannedUid { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
