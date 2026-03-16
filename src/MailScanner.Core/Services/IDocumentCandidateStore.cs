using MailScanner.Core.Models;

namespace MailScanner.Core.Services;

public interface IDocumentCandidateStore
{
    Task UpsertAsync(IEnumerable<DocumentCandidate> candidates, CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<DocumentCandidate>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<DocumentCandidate>> SearchAsync(string? searchText, CancellationToken cancellationToken = default);
}
