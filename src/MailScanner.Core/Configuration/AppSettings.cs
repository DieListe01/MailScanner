namespace MailScanner.Core.Configuration;

public sealed class AppSettings
{
    public StorageSettings Storage { get; init; } = new();
    public MailImportSettings MailImport { get; init; } = new();
}
