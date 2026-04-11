using System.Net;
using System.Text.RegularExpressions;
using MailKit;
using MailKit.Net.Imap;
using MailScanner.Core.Configuration;
using MailScanner.Core.Models;
using MailScanner.Core.Services;

namespace MailScanner.Infrastructure.Services;

public sealed class ImapMailPreviewService(IAppSettingsProvider settingsProvider) : IMailPreviewService
{
    private const int ImapTimeoutMilliseconds = 10000;

    public async Task<string> GetPlainTextPreviewAsync(DocumentCandidate candidate, CancellationToken cancellationToken = default)
    {
        var account = settingsProvider.GetCurrentSettings().MailImport.Accounts.FirstOrDefault(a =>
            string.Equals(a.EmailAddress, candidate.AccountAddress, StringComparison.OrdinalIgnoreCase)
            || string.Equals(a.DisplayName, candidate.AccountName, StringComparison.OrdinalIgnoreCase));

        if (account is null)
        {
            return "Mail-Vorschau nicht verfuegbar: Konto nicht in den aktuellen Einstellungen gefunden.";
        }

        using var client = new ImapClient();
        client.Timeout = ImapTimeoutMilliseconds;

        await client.ConnectAsync(account.ImapHost, account.ImapPort, account.UseSsl, cancellationToken);
        await client.AuthenticateAsync(account.UserName, account.Password, cancellationToken);

        try
        {
            var folder = await client.GetFolderAsync(candidate.FolderName, cancellationToken);
            await folder.OpenAsync(MailKit.FolderAccess.ReadOnly, cancellationToken);
            var message = await folder.GetMessageAsync(new MailKit.UniqueId(candidate.ImapUid), cancellationToken);

            var body = message.TextBody;
            if (string.IsNullOrWhiteSpace(body))
            {
                body = ConvertHtmlToPlainText(message.HtmlBody);
            }

            body = string.IsNullOrWhiteSpace(body)
                ? "Kein lesbarer Plain-Text-Mailinhalt verfuegbar."
                : body.Trim();

            return $@"Betreff: {message.Subject}
Absender: {message.From}
Konto: {candidate.AccountName}
Ordner: {candidate.FolderName}
Empfangen: {message.Date.LocalDateTime:dd.MM.yyyy HH:mm:ss}
Anhang: {candidate.AttachmentName}

{body}";
        }
        finally
        {
            if (client.IsConnected)
            {
                await client.DisconnectAsync(true, cancellationToken);
            }
        }
    }

    private static string ConvertHtmlToPlainText(string? html)
    {
        if (string.IsNullOrWhiteSpace(html))
        {
            return string.Empty;
        }

        var text = html;
        text = Regex.Replace(text, @"<\s*br\s*/?>", "\n", RegexOptions.IgnoreCase);
        text = Regex.Replace(text, @"<\s*/p\s*>", "\n\n", RegexOptions.IgnoreCase);
        text = Regex.Replace(text, @"<[^>]+>", " ");
        text = WebUtility.HtmlDecode(text);
        text = Regex.Replace(text, @"[ \t]+", " ");
        text = Regex.Replace(text, @"\n{3,}", "\n\n");
        return text.Trim();
    }
}
