using MailScanner.Core.Models;

namespace MailScanner.Core.Services;

public interface IMailboxScanStateStore
{
    Task<MailboxScanState?> GetAsync(string accountAddress, string folderName, CancellationToken cancellationToken = default);
    Task SaveAsync(MailboxScanState state, CancellationToken cancellationToken = default);
}
