using System.Windows;

namespace MailScanner.App;

public partial class MainWindow
{
    private void OnCopyDebugClicked(object sender, RoutedEventArgs e)
    {
        try
        {
            System.Windows.Clipboard.SetText(DebugLog.GetCombinedText());
            StatusMessage = "Debug-Uebersicht in die Zwischenablage kopiert.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Fehler beim Kopieren: {ex.Message}";
        }
    }

    private void OnClearDebugClicked(object sender, RoutedEventArgs e)
    {
        DebugLog.ClearAll();
        StatusMessage = "Debug-Ausgabe geleert.";
    }
}
