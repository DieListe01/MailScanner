using MailKit.Net.Imap;
using MailScanner.Core.Configuration;
using MailScanner.Core.Models;
using MailScanner.Core.Services;

namespace MailScanner.Infrastructure.Services;

public sealed class ImapConnectionTestService(IAppSettingsProvider settingsProvider) : IMailConnectionTestService
{
    private const int ImapTimeoutMilliseconds = 10000;

    public async Task<IReadOnlyCollection<MailAccountTestResult>> TestConnectionsAsync(CancellationToken cancellationToken = default)
    {
        var settings = settingsProvider.GetCurrentSettings().MailImport;
        var results = new List<MailAccountTestResult>();

        foreach (var account in settings.Accounts)
        {
            if (string.IsNullOrWhiteSpace(account.EmailAddress)
                || string.IsNullOrWhiteSpace(account.UserName)
                || string.IsNullOrWhiteSpace(account.Password)
                || string.IsNullOrWhiteSpace(account.ImapHost))
            {
                results.Add(new MailAccountTestResult
                {
                    DisplayName = account.DisplayName,
                    EmailAddress = account.EmailAddress,
                    Success = false,
                    Message = "Konfiguration unvollstaendig"
                });

                continue;
            }

            try
            {
                using var client = new ImapClient();
                client.Timeout = ImapTimeoutMilliseconds;

                await client.ConnectAsync(account.ImapHost, account.ImapPort, account.UseSsl, cancellationToken);
                await client.AuthenticateAsync(account.UserName, account.Password, cancellationToken);

                var folder = await client.GetFolderAsync(account.FolderName, cancellationToken);
                var folderName = folder.FullName;

                await client.DisconnectAsync(true, cancellationToken);

                results.Add(new MailAccountTestResult
                {
                    DisplayName = account.DisplayName,
                    EmailAddress = account.EmailAddress,
                    Success = true,
                    Message = $"Verbindung ok, Ordner '{folderName}' gefunden"
                });
            }
            catch (Exception ex)
            {
                results.Add(new MailAccountTestResult
                {
                    DisplayName = account.DisplayName,
                    EmailAddress = account.EmailAddress,
                    Success = false,
                    Message = ex.Message
                });
            }
        }

        return results;
    }
}
