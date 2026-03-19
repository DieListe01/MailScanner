using Microsoft.Win32;
using System.IO;
using System.Text.Json;
using MailScanner.Core.Configuration;
using MailScanner.Core.Services;

namespace MailScanner.App.Services;

public sealed class RegistryAppSettingsStore : IAppSettingsProvider
{
    private const string RegistryKey = @"SOFTWARE\MailScanner";
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = null
    };

    private AppSettings currentSettings = LoadFromRegistry();
    private bool registryWorking = true;
    private static string lastLoadSource = "Standardwerte";

    public AppSettings GetCurrentSettings()
    {
        return Clone(currentSettings);
    }

    public async Task SaveAsync(AppSettings settings, CancellationToken cancellationToken = default)
    {
        currentSettings = Normalize(Clone(settings));
        await SaveToRegistryAsync(currentSettings, cancellationToken);
    }

    public bool IsRegistryWorking() => registryWorking;
    public string GetLoadSourceSummary() => lastLoadSource;

    private static AppSettings LoadFromRegistry()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RegistryKey, false);
            if (key == null)
            {
                lastLoadSource = "Keine Registry-Daten gefunden, Standardwerte aktiv";
                return Normalize(new AppSettings());
            }

            var settingsJson = key.GetValue("Settings") as string;
            if (string.IsNullOrWhiteSpace(settingsJson))
            {
                lastLoadSource = $"Registry {RegistryKey} leer, Standardwerte aktiv";
                return Normalize(new AppSettings());
            }

            // Debug: Test JSON deserialization directly
            DebugLogService.Instance.LogRegistry($"JSON length: {settingsJson.Length}");
            DebugLogService.Instance.LogRegistry($"JSON preview: {settingsJson.Substring(0, Math.Min(200, settingsJson.Length))}...");
            
            var settings = JsonSerializer.Deserialize<AppSettings>(settingsJson, SerializerOptions);
            DebugLogService.Instance.LogRegistry($"Deserialized successfully: {settings != null}");
            DebugLogService.Instance.LogRegistry($"Accounts count: {settings?.MailImport.Accounts?.Count ?? 0}");
            lastLoadSource = $"Registry HKCU\\{RegistryKey}";
            
            return Normalize(settings ?? new AppSettings());
        }
        catch
        {
            // Fallback to JSON file if registry access fails
            try
            {
                var jsonPath = Path.Combine(AppDataPaths.GetUserDataDirectory(), "appsettings.json");
                if (File.Exists(jsonPath))
                {
                    var json = File.ReadAllText(jsonPath);
                    var settings = JsonSerializer.Deserialize<AppSettings>(json, SerializerOptions);
                    lastLoadSource = $"JSON-Backup {jsonPath}";
                    return Normalize(settings ?? new AppSettings());
                }
            }
            catch
            {
                // Final fallback
            }
            lastLoadSource = "Standardwerte (weder Registry noch JSON verfuegbar)";
            return Normalize(new AppSettings());
        }
    }

    private async Task SaveToRegistryAsync(AppSettings settings, CancellationToken cancellationToken)
    {
        await Task.Run(() =>
        {
            try
            {
                using var key = Registry.CurrentUser.CreateSubKey(RegistryKey, true);
                var settingsJson = JsonSerializer.Serialize(settings, SerializerOptions);
                key.SetValue("Settings", settingsJson, RegistryValueKind.String);
                key.SetValue("LastSaved", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"), RegistryValueKind.String);
                
                // Also save to JSON as backup
                var jsonPath = Path.Combine(AppDataPaths.GetUserDataDirectory(), "appsettings.json");
                var directory = Path.GetDirectoryName(jsonPath);
                if (!string.IsNullOrWhiteSpace(directory))
                {
                    Directory.CreateDirectory(directory);
                }
                File.WriteAllText(jsonPath, settingsJson);
            }
            catch
            {
                // Fallback to JSON file
                try
                {
                    var jsonPath = Path.Combine(AppDataPaths.GetUserDataDirectory(), "appsettings.json");
                    var directory = Path.GetDirectoryName(jsonPath);
                    if (!string.IsNullOrWhiteSpace(directory))
                    {
                        Directory.CreateDirectory(directory);
                    }
                    var settingsJson = JsonSerializer.Serialize(settings, SerializerOptions);
                    File.WriteAllText(jsonPath, settingsJson);
                }
                catch
                {
                    // Silent fail - settings will be loaded from defaults next time
                }
            }
        }, cancellationToken);
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
                Accounts = settings.MailImport.Accounts ?? []
            }
        };
    }
}
