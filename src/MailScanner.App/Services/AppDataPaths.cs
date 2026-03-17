using System.IO;

namespace MailScanner.App.Services;

public static class AppDataPaths
{
    private const string AppFolderName = "MailScanner";

    public static string GetUserDataDirectory()
    {
        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            AppFolderName);
    }

    public static string GetUserSettingsFilePath()
    {
        return Path.Combine(GetUserDataDirectory(), "settings.ini");
    }

    public static string GetBundledSettingsFilePath()
    {
        return Path.Combine(AppContext.BaseDirectory, "settings.ini");
    }

    public static void EnsureUserSettingsFileSeeded()
    {
        var userSettingsFilePath = GetUserSettingsFilePath();
        if (File.Exists(userSettingsFilePath))
        {
            return;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(userSettingsFilePath)!);

        var bundledSettingsFilePath = GetBundledSettingsFilePath();
        if (File.Exists(bundledSettingsFilePath))
        {
            File.Copy(bundledSettingsFilePath, userSettingsFilePath, overwrite: false);
        }
    }
}
