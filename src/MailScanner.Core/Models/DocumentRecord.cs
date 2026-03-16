using MailScanner.Core.Enums;

namespace MailScanner.Core.Models;

public sealed class DocumentRecord
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid CandidateId { get; init; }
    public string FilePath { get; init; } = string.Empty;
    public string FileHash { get; init; } = string.Empty;
    public string OriginalFileName { get; init; } = string.Empty;
    public string Sender { get; init; } = string.Empty;
    public string InvoiceNumber { get; init; } = string.Empty;
    public decimal? Amount { get; init; }
    public DateOnly? DocumentDate { get; init; }
    public DateTimeOffset DownloadedAt { get; init; } = DateTimeOffset.UtcNow;
    public DocumentCategory Category { get; init; } = DocumentCategory.Unknown;
}
