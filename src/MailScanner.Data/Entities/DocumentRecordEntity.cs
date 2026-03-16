namespace MailScanner.Data.Entities;

public sealed class DocumentRecordEntity
{
    public Guid Id { get; set; }
    public Guid CandidateId { get; set; }
    public string FilePath { get; set; } = string.Empty;
    public string FileHash { get; set; } = string.Empty;
    public string OriginalFileName { get; set; } = string.Empty;
    public string Sender { get; set; } = string.Empty;
    public string InvoiceNumber { get; set; } = string.Empty;
    public decimal? Amount { get; set; }
    public DateOnly? DocumentDate { get; set; }
    public DateTimeOffset DownloadedAt { get; set; }
    public int Category { get; set; }
}
