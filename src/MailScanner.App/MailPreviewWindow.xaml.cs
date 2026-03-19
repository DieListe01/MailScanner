using MailScanner.App.Models;
using MailScanner.Core.Models;
using System.Windows;
using System.Windows.Input;

namespace MailScanner.App;

public partial class MailPreviewWindow : System.Windows.Window
{
    private readonly DocumentCandidate candidate;

    public MailPreviewWindow(DocumentCandidate candidate)
    {
        InitializeComponent();
        this.candidate = candidate;
        
        // Get mail import service through dependency injection or create simple mock
        // For now, we'll show basic info
        DataContext = new
        {
            Subject = candidate.Subject,
            Sender = candidate.Sender,
            AccountName = candidate.AccountName,
            FolderName = candidate.FolderName,
            ReceivedAtDisplay = candidate.ReceivedAt.LocalDateTime.ToString("dd.MM.yyyy HH:mm"),
            AttachmentName = candidate.AttachmentName,
            MatchReason = CandidateListItem.FromCandidate(candidate).MatchReason,
            StatusLabel = candidate.Status.ToString(),
            CategoryLabel = $"Kategorie: {candidate.SuggestedCategory}"
        };

        // Load mail content asynchronously
        Loaded += async (s, e) => await LoadMailContentAsync();
    }

    private void OnTitleBarMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Left)
        {
            return;
        }

        if (e.ClickCount == 2 && ResizeMode == ResizeMode.CanResize)
        {
            WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
            return;
        }

        DragMove();
    }

    private void OnMinimizeWindowClicked(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    private void OnToggleMaximizeWindowClicked(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
    }

    private async Task LoadMailContentAsync()
    {
        try
        {
            // For now, show placeholder content
            // In a real implementation, you would fetch the actual mail content
            await Dispatcher.InvokeAsync(() =>
            {
                MailContentText.Text = $@"Mail-Inhalt wird geladen...

Betreff: {candidate.Subject}
Absender: {candidate.Sender}
Konto: {candidate.AccountName}
Ordner: {candidate.FolderName}
Empfangen: {candidate.ReceivedAt.LocalDateTime:dd.MM.yyyy HH:mm:ss}
Anhang: {candidate.AttachmentName} ({candidate.AttachmentSizeInBytes} Bytes)

Status: {candidate.Status}
Kategorie: {candidate.SuggestedCategory}

Hinweis: Der vollständige Mail-Text wird in einer zukünftigen Version geladen.
Derzeit werden nur die Metadaten angezeigt.";
            });
        }
        catch (Exception ex)
        {
            await Dispatcher.InvokeAsync(() =>
            {
                MailContentText.Text = $"Fehler beim Laden des Mail-Inhalts: {ex.Message}";
            });
        }
    }

    private void OnOpenAttachmentClicked(object sender, System.Windows.RoutedEventArgs e)
    {
        try
        {
            // Try to open the attachment file
            var filePath = System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                "MailScanner",
                candidate.AccountName,
                candidate.AttachmentName);

            if (System.IO.File.Exists(filePath))
            {
                var process = new System.Diagnostics.Process
                {
                    StartInfo = new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = filePath,
                        UseShellExecute = true
                    }
                };
                process.Start();
                Close();
            }
            else
            {
                System.Windows.MessageBox.Show(
                    $"Die Datei wurde nicht gefunden:\n{filePath}\n\nBitte lade das Dokument zuerst herunter.",
                    "Datei nicht gefunden",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Warning);
            }
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show(
                $"Fehler beim Öffnen der Datei: {ex.Message}",
                "Fehler",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Error);
        }
    }

    private void OnCloseClicked(object sender, System.Windows.RoutedEventArgs e)
    {
        Close();
    }
}
