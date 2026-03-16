namespace MailScanner.Core.Configuration;

public sealed class StorageSettings
{
    public string DatabasePath { get; init; } = string.Empty;
    public string DocumentRootPath { get; init; } = string.Empty;
}
