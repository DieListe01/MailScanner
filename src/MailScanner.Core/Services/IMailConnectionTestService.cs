using MailScanner.Core.Models;

namespace MailScanner.Core.Services;

public interface IMailConnectionTestService
{
    Task<IReadOnlyCollection<MailAccountTestResult>> TestConnectionsAsync(CancellationToken cancellationToken = default);
}
