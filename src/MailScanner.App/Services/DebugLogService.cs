using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text;

namespace MailScanner.App.Services;

public sealed class DebugLogService : INotifyPropertyChanged
{
    private readonly StringBuilder generalBuilder = new();
    private readonly StringBuilder registryBuilder = new();
    private readonly StringBuilder settingsBuilder = new();
    private readonly StringBuilder errorBuilder = new();
    private string statusText = "Bereit";
    private string timestampText = string.Empty;

    public static DebugLogService Instance { get; } = new();

    public string GeneralText => generalBuilder.ToString();
    public string RegistryText => registryBuilder.ToString();
    public string SettingsText => settingsBuilder.ToString();
    public string ErrorText => errorBuilder.ToString();

    public string StatusText
    {
        get => statusText;
        private set
        {
            statusText = value;
            OnPropertyChanged();
        }
    }

    public string TimestampText
    {
        get => timestampText;
        private set
        {
            timestampText = value;
            OnPropertyChanged();
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public void LogGeneral(string message) => Append(generalBuilder, nameof(GeneralText), "Allgemein", message);
    public void LogRegistry(string message) => Append(registryBuilder, nameof(RegistryText), "Registry", message);
    public void LogSettings(string message) => Append(settingsBuilder, nameof(SettingsText), "Settings", message);
    public void LogError(string message) => Append(errorBuilder, nameof(ErrorText), "Fehler", $"ERROR: {message}");

    public string GetTextForTab(int tabIndex) => tabIndex switch
    {
        0 => GeneralText,
        1 => RegistryText,
        2 => SettingsText,
        3 => ErrorText,
        _ => GeneralText
    };

    public string GetCombinedText()
    {
        return string.Join(Environment.NewLine + Environment.NewLine,
        [
            "[Allgemein]" + Environment.NewLine + GeneralText,
            "[Registry]" + Environment.NewLine + RegistryText,
            "[Settings]" + Environment.NewLine + SettingsText,
            "[Fehler]" + Environment.NewLine + ErrorText
        ]);
    }

    public void ClearAll()
    {
        generalBuilder.Clear();
        registryBuilder.Clear();
        settingsBuilder.Clear();
        errorBuilder.Clear();
        OnPropertyChanged(nameof(GeneralText));
        OnPropertyChanged(nameof(RegistryText));
        OnPropertyChanged(nameof(SettingsText));
        OnPropertyChanged(nameof(ErrorText));
        UpdateStatus("Geloescht", "Alle Debug-Bereiche wurden geleert");
    }

    public void ClearTab(int tabIndex)
    {
        switch (tabIndex)
        {
            case 0:
                generalBuilder.Clear();
                OnPropertyChanged(nameof(GeneralText));
                break;
            case 1:
                registryBuilder.Clear();
                OnPropertyChanged(nameof(RegistryText));
                break;
            case 2:
                settingsBuilder.Clear();
                OnPropertyChanged(nameof(SettingsText));
                break;
            case 3:
                errorBuilder.Clear();
                OnPropertyChanged(nameof(ErrorText));
                break;
        }

        UpdateStatus("Geloescht", $"Tab {tabIndex + 1} wurde geleert");
    }

    private void Append(StringBuilder builder, string propertyName, string category, string message)
    {
        var timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
        builder.Append('[').Append(timestamp).Append("] ").AppendLine(message);
        OnPropertyChanged(propertyName);
        UpdateStatus(category, message);
    }

    private void UpdateStatus(string category, string message)
    {
        StatusText = $"[{category}] {message}";
        TimestampText = $"Letzte Aktualisierung: {DateTime.Now:HH:mm:ss}";
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
