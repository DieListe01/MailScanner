using MailScanner.Core.Models;

namespace MailScanner.Core.Services;

public interface IMailImportService
{
    Task<IReadOnlyCollection<DocumentCandidate>> ImportNewCandidatesAsync(IProgress<MailImportProgress>? progress = null, CancellationToken cancellationToken = default);
}
