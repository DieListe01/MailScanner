using System.Linq;
using MailScanner.App.Models;
using MailScanner.Core.Models;

namespace MailScanner.App;

public partial class MainWindow
{
    private DocumentCandidate? previewCandidate;
    private string previewSubject = "Keine Vorschau geladen";
    private string previewSender = string.Empty;
    private string previewAccountName = string.Empty;
    private string previewFolderName = string.Empty;
    private string previewReceivedAtDisplay = string.Empty;
    private string previewAttachmentName = string.Empty;
    private string previewMatchReason = string.Empty;
    private string previewStatusLabel = string.Empty;
    private string previewCategoryLabel = string.Empty;
    private string previewContentText = "Waehle einen Treffer fuer die Vorschau aus.";

    public string PreviewSubject { get => previewSubject; set { previewSubject = value; OnPropertyChanged(); } }
    public string PreviewSender { get => previewSender; set { previewSender = value; OnPropertyChanged(); } }
    public string PreviewAccountName { get => previewAccountName; set { previewAccountName = value; OnPropertyChanged(); } }
    public string PreviewFolderName { get => previewFolderName; set { previewFolderName = value; OnPropertyChanged(); } }
    public string PreviewReceivedAtDisplay { get => previewReceivedAtDisplay; set { previewReceivedAtDisplay = value; OnPropertyChanged(); } }
    public string PreviewAttachmentName { get => previewAttachmentName; set { previewAttachmentName = value; OnPropertyChanged(); } }
    public string PreviewMatchReason { get => previewMatchReason; set { previewMatchReason = value; OnPropertyChanged(); } }
    public string PreviewStatusLabel { get => previewStatusLabel; set { previewStatusLabel = value; OnPropertyChanged(); } }
    public string PreviewCategoryLabel { get => previewCategoryLabel; set { previewCategoryLabel = value; OnPropertyChanged(); } }
    public string PreviewContentText { get => previewContentText; set { previewContentText = value; OnPropertyChanged(); } }

    private void ShowPreview(DocumentCandidate candidate)
    {
        previewCandidate = candidate;
        var listItem = CandidateListItem.FromCandidate(candidate);

        PreviewSubject = candidate.Subject;
        PreviewSender = candidate.Sender;
        PreviewAccountName = candidate.AccountName;
        PreviewFolderName = candidate.FolderName;
        PreviewReceivedAtDisplay = candidate.ReceivedAt.LocalDateTime.ToString("dd.MM.yyyy HH:mm");
        PreviewAttachmentName = candidate.AttachmentName;
        PreviewMatchReason = listItem.MatchReason;
        PreviewStatusLabel = candidate.Status.ToString();
        PreviewCategoryLabel = $"Kategorie: {candidate.SuggestedCategory}";
        PreviewContentText = $@"Mail-Inhalt wird geladen...

Betreff: {candidate.Subject}
Absender: {candidate.Sender}
Konto: {candidate.AccountName}
Ordner: {candidate.FolderName}
Empfangen: {candidate.ReceivedAt.LocalDateTime:dd.MM.yyyy HH:mm:ss}
Anhang: {candidate.AttachmentName} ({candidate.AttachmentSizeInBytes} Bytes)

Status: {candidate.Status}
Kategorie: {candidate.SuggestedCategory}

Hinweis: Der vollstaendige Mail-Text wird in einer spaeteren Ausbaustufe direkt aus dem Import geladen.
Derzeit werden die lokal verfuegbaren Metadaten angezeigt.";

        PreviewTabVisibility = System.Windows.Visibility.Visible;
        SetCurrentPage(WorkspacePage.Results);
        ResultsTabs.SelectedItem = PreviewTabItem;
        StatusMessage = $"Vorschau geladen: {candidate.Subject}";
    }

    private void OnOpenPreviewAttachmentClicked(object sender, System.Windows.RoutedEventArgs e)
    {
        if (previewCandidate is null)
        {
            return;
        }

        OpenCandidateAttachment(previewCandidate);
    }

    private void OpenCandidateAttachment(DocumentCandidate candidate)
    {
        try
        {
            if (candidate.AttachmentName.Equals("[Email-Text]", StringComparison.OrdinalIgnoreCase))
            {
                ShowPreview(candidate);
                return;
            }

            var possiblePaths = new[]
            {
                System.IO.Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                    "MailScanner",
                    candidate.AccountName,
                    candidate.AttachmentName),
                System.IO.Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                    "MailScanner",
                    candidate.AccountAddress,
                    candidate.AttachmentName),
                candidate.StoredFilePath
            };

            var foundPath = possiblePaths.FirstOrDefault(path => !string.IsNullOrEmpty(path) && System.IO.File.Exists(path));
            if (foundPath != null)
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = foundPath,
                    UseShellExecute = true
                });
                StatusMessage = $"Dokument geoeffnet: {candidate.AttachmentName}";
                return;
            }

            StatusMessage = $"Dokument nicht gefunden: {candidate.AttachmentName}. Bitte zuerst herunterladen.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Fehler beim Oeffnen: {ex.Message}";
        }
    }
}
