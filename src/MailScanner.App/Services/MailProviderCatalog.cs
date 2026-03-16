using MailScanner.App.Models;

namespace MailScanner.App.Services;

public static class MailProviderCatalog
{
    public static IReadOnlyList<MailProviderPreset> All { get; } =
    [
        new MailProviderPreset
        {
            Name = "Gmail",
            ImapHost = "imap.gmail.com",
            ImapPort = 993,
            UseSsl = true,
            FolderName = "INBOX",
            Hint = "Gmail braucht in der Regel ein App-Passwort, wenn 2-Faktor-Anmeldung aktiv ist."
        },
        new MailProviderPreset
        {
            Name = "Outlook / Microsoft 365",
            ImapHost = "outlook.office365.com",
            ImapPort = 993,
            UseSsl = true,
            FolderName = "INBOX",
            Hint = "Bei Microsoft-Konten wird haeufig ein App-Passwort oder moderne Anmeldung benoetigt."
        },
        new MailProviderPreset
        {
            Name = "GMX",
            ImapHost = "imap.gmx.net",
            ImapPort = 993,
            UseSsl = true,
            FolderName = "INBOX",
            Hint = "Bei GMX ist der Benutzername meist die komplette E-Mail-Adresse."
        },
        new MailProviderPreset
        {
            Name = "WEB.DE",
            ImapHost = "imap.web.de",
            ImapPort = 993,
            UseSsl = true,
            FolderName = "INBOX",
            Hint = "Bei WEB.DE ist der Benutzername meist die komplette E-Mail-Adresse."
        },
        new MailProviderPreset
        {
            Name = "Yahoo",
            ImapHost = "imap.mail.yahoo.com",
            ImapPort = 993,
            UseSsl = true,
            FolderName = "INBOX",
            Hint = "Yahoo verlangt oft ein App-Passwort fuer IMAP-Zugriffe."
        },
        new MailProviderPreset
        {
            Name = "mailbox.org",
            ImapHost = "imap.mailbox.org",
            ImapPort = 993,
            UseSsl = true,
            FolderName = "INBOX",
            Hint = "mailbox.org arbeitet in der Regel direkt mit der Mailadresse als Benutzername."
        },
        new MailProviderPreset
        {
            Name = "Benutzerdefiniert",
            ImapHost = string.Empty,
            ImapPort = 993,
            UseSsl = true,
            FolderName = "INBOX",
            Hint = "Fuer eigene Provider bitte Host, Port und Ordner manuell eintragen."
        }
    ];

    public static MailProviderPreset GetByName(string? name)
    {
        return All.FirstOrDefault(x => x.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
            ?? All[^1];
    }
}
