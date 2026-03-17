using System.IO;
using MailScanner.Core.Configuration;
using MailScanner.Core.Services;

namespace MailScanner.App.Services;

/// <summary>
/// Settings provider that stores DatabasePath and DocumentRootPath in an INI file.
/// </summary>
public sealed class IniAppSettingsStore(string settingsFilePath) : IAppSettingsProvider
{
    private AppSettings currentSettings = LoadFromDisk(settingsFilePath);

    public AppSettings GetCurrentSettings()
    {
        return Clone(currentSettings);
    }

    public async Task SaveAsync(AppSettings settings, CancellationToken cancellationToken = default)
    {
        currentSettings = Normalize(Clone(settings));

        var directory = Path.GetDirectoryName(settingsFilePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var iniContent = $"[Settings]{System.Environment.NewLine}DatabasePath={settings.Storage.DatabasePath}{System.Environment.NewLine}DocumentRootPath={settings.Storage.DocumentRootPath}{System.Environment.NewLine}";

        await using var stream = File.Create(settingsFilePath);
        await stream.WriteAsync(System.Text.Encoding.UTF8.GetBytes(iniContent), cancellationToken);
    }

    private static AppSettings LoadFromDisk(string settingsFilePath)
    {
        if (!File.Exists(settingsFilePath))
        {
            return Normalize(new AppSettings());
        }

        try
        {
            var content = File.ReadAllText(settingsFilePath);
            string databasePath = null;
            string documentRootPath = null;
            foreach (var line in content.Split(new[] { '\r', '\n' }, System.StringSplitOptions.RemoveEmptyEntries))
            {
                var trimmed = line.Trim();
                if (trimmed.StartsWith("DatabasePath=", System.StringComparison.OrdinalIgnoreCase))
                {
                    databasePath = trimmed.Substring("DatabasePath=".Length).Trim();
                }
                else if (trimmed.StartsWith("DocumentRootPath=", System.StringComparison.OrdinalIgnoreCase))
                {
                    documentRootPath = trimmed.Substring("DocumentRootPath=".Length).Trim();
                }
            }

            var settings = new AppSettings
            {
                Storage = new StorageSettings
                {
                    DatabasePath = string.IsNullOrWhiteSpace(databasePath)
                        ? Path.Combine(AppDataPaths.GetUserDataDirectory(), "storage", "mailscanner.db")
                        : databasePath,
                    DocumentRootPath = string.IsNullOrWhiteSpace(documentRootPath)
                        ? Path.Combine(AppDataPaths.GetUserDataDirectory(), "storage", "documents")
                        : documentRootPath
                },
                MailImport = new MailImportSettings()
            };
            return Normalize(settings);
        }
        catch
        {
            // fall through to default
        }

        return Normalize(new AppSettings());
    }

    private static AppSettings Clone(AppSettings settings)
    {
        var storageRoot = Path.Combine(AppDataPaths.GetUserDataDirectory(), "storage");

        return new AppSettings
        {
            Storage = new StorageSettings
            {
                DatabasePath = string.IsNullOrWhiteSpace(settings.Storage.DatabasePath)
                    ? Path.Combine(storageRoot, "mailscanner.db")
                    : settings.Storage.DatabasePath,
                DocumentRootPath = string.IsNullOrWhiteSpace(settings.Storage.DocumentRootPath)
                    ? Path.Combine(storageRoot, "documents")
                    : settings.Storage.DocumentRootPath
            },
            MailImport = new MailImportSettings
            {
                InitialLookbackDays = settings.MailImport.InitialLookbackDays,
                ExcludedFolderPatterns = settings.MailImport.ExcludedFolderPatterns
                    .Where(folder => !string.IsNullOrWhiteSpace(folder))
                    .Select(folder => folder.Trim())
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToArray(),
                Accounts = settings.MailImport.Accounts
            }
        };
    }

    private static AppSettings Normalize(AppSettings settings)
    {
        var storageRoot = Path.Combine(AppDataPaths.GetUserDataDirectory(), "storage");

        return new AppSettings
        {
            Storage = new StorageSettings
            {
                DatabasePath = string.IsNullOrWhiteSpace(settings.Storage.DatabasePath)
                    ? Path.Combine(storageRoot, "mailscanner.db")
                    : settings.Storage.DatabasePath,
                DocumentRootPath = string.IsNullOrWhiteSpace(settings.Storage.DocumentRootPath)
                    ? Path.Combine(storageRoot, "documents")
                    : settings.Storage.DocumentRootPath
            },
            MailImport = new MailImportSettings
            {
                InitialLookbackDays = settings.MailImport.InitialLookbackDays,
                ExcludedFolderPatterns = settings.MailImport.ExcludedFolderPatterns
                    .Where(folder => !string.IsNullOrWhiteSpace(folder))
                    .Select(folder => folder.Trim())
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToArray(),
                Accounts = settings.MailImport.Accounts
            }
        };
    }
}