namespace MailScanner.Core.Models;

public sealed class DocumentDownloadProgress
{
    public int CompletedCount { get; init; }
    public int TotalCount { get; init; }
    public string CurrentFileName { get; init; } = string.Empty;
    public bool HasError { get; init; }
}
