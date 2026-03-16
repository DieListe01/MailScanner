namespace MailScanner.Core.Models;

public sealed class DownloadDocumentsResult
{
    public IReadOnlyCollection<DocumentRecord> DownloadedDocuments { get; init; } = [];
    public IReadOnlyCollection<string> Errors { get; init; } = [];
}
