using System.IO;
using System.Text.Json;
using MailScanner.Core.Configuration;
using MailScanner.Core.Services;

namespace MailScanner.App.Services;

/// <summary>
/// Settings provider that stores the entire AppSettings as a JSON string in an INI file.
/// </summary>
public sealed class IniAppSettingsStore(string settingsFilePath) : IAppSettingsProvider
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = false, // compact JSON to avoid newlines
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

        var json = JsonSerializer.Serialize(currentSettings, SerializerOptions);
        var iniContent = $"[App]{System.Environment.NewLine}Settings={json}{System.Environment.NewLine}";

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
            // Find the line that starts with Settings=
            var lines = content.Split(new[] { '\r', '\n' }, System.StringSplitOptions.RemoveEmptyEntries);
            string? jsonValue = null;
            foreach (var line in lines)
            {
                var trimmed = line.Trim();
                if (trimmed.StartsWith("Settings=", System.StringComparison.OrdinalIgnoreCase))
                {
                    jsonValue = trimmed.Substring("Settings=".Length).Trim();
                    break;
                }
            }

            if (!string.IsNullOrWhiteSpace(jsonValue))
            {
                var settings = JsonSerializer.Deserialize<AppSettings>(jsonValue, SerializerOptions);
                if (settings != null)
                {
                    return Normalize(settings);
                }
            }
        }
        catch
        {
            // fall through to default
        }

        return Normalize(new AppSettings());
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