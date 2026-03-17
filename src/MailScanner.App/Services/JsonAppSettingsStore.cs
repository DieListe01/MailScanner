using System.IO;
using System.Text.Json;
using MailScanner.Core.Configuration;
using MailScanner.Core.Services;

namespace MailScanner.App.Services;

public sealed class JsonAppSettingsStore(string settingsFilePath) : IAppSettingsProvider
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = null
    };

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

        await using var stream = File.Create(settingsFilePath);
        await JsonSerializer.SerializeAsync(stream, currentSettings, SerializerOptions, cancellationToken);
    }

    private static AppSettings LoadFromDisk(string settingsFilePath)
    {
        if (!File.Exists(settingsFilePath))
        {
            return Normalize(new AppSettings());
        }

        var json = File.ReadAllText(settingsFilePath);
        return Normalize(JsonSerializer.Deserialize<AppSettings>(json, SerializerOptions) ?? new AppSettings());
    }

    private static AppSettings Clone(AppSettings settings)
    {
        var json = JsonSerializer.Serialize(settings, SerializerOptions);
        return JsonSerializer.Deserialize<AppSettings>(json, SerializerOptions) ?? new AppSettings();
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
