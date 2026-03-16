namespace MailScanner.App.Models;

public sealed class MailProviderPreset
{
    public required string Name { get; init; }
    public required string ImapHost { get; init; }
    public required int ImapPort { get; init; }
    public required bool UseSsl { get; init; }
    public required string FolderName { get; init; }
    public required string Hint { get; init; }
}
