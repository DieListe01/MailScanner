using MailScanner.Core.Models;

namespace MailScanner.Core.Services;

public interface IMailPreviewService
{
    Task<string> GetPlainTextPreviewAsync(DocumentCandidate candidate, CancellationToken cancellationToken = default);
}
