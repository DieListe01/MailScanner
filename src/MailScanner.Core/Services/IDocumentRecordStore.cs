using MailScanner.Core.Models;

namespace MailScanner.Core.Services;

public interface IDocumentRecordStore
{
    Task SaveAsync(IEnumerable<DocumentRecord> documents, CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<DocumentRecord>> GetAllAsync(CancellationToken cancellationToken = default);
}
