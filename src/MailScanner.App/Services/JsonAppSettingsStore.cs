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
        currentSettings = Clone(settings);

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
            return new AppSettings();
        }

        var json = File.ReadAllText(settingsFilePath);
        return JsonSerializer.Deserialize<AppSettings>(json, SerializerOptions) ?? new AppSettings();
    }

    private static AppSettings Clone(AppSettings settings)
    {
        var json = JsonSerializer.Serialize(settings, SerializerOptions);
        return JsonSerializer.Deserialize<AppSettings>(json, SerializerOptions) ?? new AppSettings();
    }
}
