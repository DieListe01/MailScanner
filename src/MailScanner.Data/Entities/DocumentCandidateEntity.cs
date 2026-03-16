namespace MailScanner.Data.Entities;

public sealed class DocumentCandidateEntity
{
    public Guid Id { get; set; }
    public string AccountName { get; set; } = string.Empty;
    public string AccountAddress { get; set; } = string.Empty;
    public string FolderName { get; set; } = string.Empty;
    public uint ImapUid { get; set; }
    public string MessageId { get; set; } = string.Empty;
    public string Sender { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public DateTimeOffset ReceivedAt { get; set; }
    public string AttachmentName { get; set; } = string.Empty;
    public long AttachmentSizeInBytes { get; set; }
    public int SuggestedCategory { get; set; }
    public int Status { get; set; }
    public string StoredFilePath { get; set; } = string.Empty;
}
