using MailScanner.Core.Configuration;

namespace MailScanner.Core.Services;

public interface IAppSettingsProvider
{
    AppSettings GetCurrentSettings();
    Task SaveAsync(AppSettings settings, CancellationToken cancellationToken = default);
}
