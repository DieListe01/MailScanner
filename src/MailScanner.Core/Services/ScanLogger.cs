using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace MailScanner.Core.Services;

public class ScanLogger
{
    private readonly string logFilePath;
    private readonly StringBuilder logBuilder = new();

    public event Action? LogChanged;

    public ScanLogger()
    {
        var logDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MailScanner", "Logs");
        Directory.CreateDirectory(logDir);
        logFilePath = Path.Combine(logDir, $"scan_{DateTime.Now:yyyyMMdd_HHmmss}.log");
    }

    public void LogInfo(string message)
    {
        var timestamp = DateTime.Now.ToString("HH:mm:ss");
        var logEntry = $"[{timestamp}] [INFO] {message}";
        logBuilder.AppendLine(logEntry);
        Console.WriteLine(logEntry);
        LogChanged?.Invoke();
    }

    public void LogWarning(string message)
    {
        var timestamp = DateTime.Now.ToString("HH:mm:ss");
        var logEntry = $"[{timestamp}] [WARN] {message}";
        logBuilder.AppendLine(logEntry);
        Console.WriteLine(logEntry);
        LogChanged?.Invoke();
    }

    public void LogError(string message, Exception? ex = null)
    {
        var timestamp = DateTime.Now.ToString("HH:mm:ss");
        var logEntry = $"[{timestamp}] [ERROR] {message}";
        if (ex != null)
        {
            logEntry += $"\nException: {ex.Message}";
        }
        logBuilder.AppendLine(logEntry);
        Console.WriteLine(logEntry);
        LogChanged?.Invoke();
    }

    public void LogMail(string account, string folder, string subject, string sender, bool hasAttachment, bool isPdf, bool isInvoice)
    {
        var timestamp = DateTime.Now.ToString("HH:mm:ss");
        var attachmentInfo = hasAttachment ? $"[ANHANG: {(isPdf ? "PDF" : "OTHER")}]" : "[KEIN ANHANG]";
        var invoiceInfo = isInvoice ? "[RECHNUNG]" : "";
        var logEntry = $"[{timestamp}] [MAIL] {account}/{folder}: {sender} - {subject} {attachmentInfo}{invoiceInfo}";
        logBuilder.AppendLine(logEntry);
        LogChanged?.Invoke();
    }

    public string GetLogText() => logBuilder.ToString();

    public async Task SaveLogAsync()
    {
        try
        {
            await File.WriteAllTextAsync(logFilePath, logBuilder.ToString());
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to save log: {ex.Message}");
        }
    }

    public string GetLogFilePath() => logFilePath;
}
