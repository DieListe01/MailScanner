using MailScanner.Core.Models;

namespace MailScanner.Core.Services;

public interface IDocumentDownloadService
{
    Task<DownloadDocumentsResult> DownloadAsync(IEnumerable<DocumentCandidate> candidates, IProgress<DocumentDownloadProgress>? progress = null, CancellationToken cancellationToken = default);
}
