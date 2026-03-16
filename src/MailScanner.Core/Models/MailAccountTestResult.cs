namespace MailScanner.Core.Models;

public sealed class MailAccountTestResult
{
    public string DisplayName { get; init; } = string.Empty;
    public string EmailAddress { get; init; } = string.Empty;
    public bool Success { get; init; }
    public string Message { get; init; } = string.Empty;
}
