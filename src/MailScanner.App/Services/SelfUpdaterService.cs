using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

namespace MailScanner.App.Services;

/// <summary>
/// Helper service for self-update scenarios where the application needs to be replaced.
/// This is typically used when the installer needs to replace running files.
/// </summary>
public static class SelfUpdaterService
{
    /// <summary>
    /// Creates and starts a batch file that will update the application after it closes.
    /// </summary>
    /// <param name="installerPath">Path to the installer executable</param>
    /// <param name="appPath">Path to the application executable to update</param>
    /// <returns>True if the updater was successfully started</returns>
    public static bool StartDelayedUpdate(string installerPath, string? appPath = null)
    {
        try
        {
            // Use the current executable path if not provided
            if (appPath == null)
            {
                appPath = Process.GetCurrentProcess().MainModule?.FileName;
                if (string.IsNullOrEmpty(appPath))
                {
                    return false;
                }
            }

            // Create a temporary batch file that will:
            // 1. Wait for the application to close
            // 2. Run the installer
            // 3. Restart the application
            // 4. Clean up the batch file
            string batchFile = Path.Combine(Path.GetTempPath(), $"MailScanner_Update_{Guid.NewGuid():N}.bat");
            
            string batchContent = $"@echo off\r\n" +
                                $"title MailScanner Update\r\n" +
                                $"echo Waiting for MailScanner to close...\r\n" +
                                $":waitloop\r\n" +
                                $"tasklist /FI \"IMAGENAME eq MailScanner.exe\" 2>NUL | find /I /N \"MailScanner.exe\">NUL\r\n" +
                                $"if not errorlevel 1 (\r\n" +
                                $"    timeout /t 2 > nul\r\n" +
                                $"    goto waitloop\r\n" +
                                $")\r\n" +
                                $"echo MailScanner closed. Starting installer...\r\n" +
                                $"timeout /t 2 > nul\r\n" +
                                $"\"{installerPath}\"\r\n" +
                                $"echo Installer finished. Cleaning up...\r\n" +
                                $"del \"{batchFile}\"\r\n";

            File.WriteAllText(batchFile, batchContent);

            // Start the batch file in a hidden window
            Process.Start(new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = $"/c \"{batchFile}\"",
                WindowStyle = ProcessWindowStyle.Hidden,
                CreateNoWindow = true
            });

            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }
}