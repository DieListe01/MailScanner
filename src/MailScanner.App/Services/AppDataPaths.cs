using System.IO;
using System.Text;

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

        var baseDir = AppContext.BaseDirectory;
        var dbPath = Path.Combine(baseDir, "storage", "mailscanner.db");
        var docPath = Path.Combine(baseDir, "documents");

        var iniContent = $"[Settings]{System.Environment.NewLine}DatabasePath={dbPath}{System.Environment.NewLine}DocumentRootPath={docPath}{System.Environment.NewLine}";
        File.WriteAllText(userSettingsFilePath, iniContent, Encoding.UTF8);
    }
}
