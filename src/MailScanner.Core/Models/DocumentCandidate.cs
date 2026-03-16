using MailScanner.Core.Enums;

namespace MailScanner.Core.Models;

public sealed class DocumentCandidate
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string AccountName { get; init; } = string.Empty;
    public string AccountAddress { get; init; } = string.Empty;
    public string FolderName { get; init; } = string.Empty;
    public uint ImapUid { get; init; }
    public string MessageId { get; init; } = string.Empty;
    public string Sender { get; init; } = string.Empty;
    public string Subject { get; init; } = string.Empty;
    public DateTimeOffset ReceivedAt { get; init; }
    public string AttachmentName { get; init; } = string.Empty;
    public long AttachmentSizeInBytes { get; init; }
    public DocumentCategory SuggestedCategory { get; init; } = DocumentCategory.Unknown;
    public DocumentCandidateStatus Status { get; init; } = DocumentCandidateStatus.Pending;
    public string StoredFilePath { get; init; } = string.Empty;
    public bool AlreadyDownloaded => Status == DocumentCandidateStatus.Downloaded;
}
