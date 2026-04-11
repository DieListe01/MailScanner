using System.Linq;
using MailScanner.App.Models;
using MailScanner.Core.Models;
using System.Windows;

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
    private Visibility previewMetaVisibility = Visibility.Collapsed;

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
    public Visibility PreviewMetaVisibility
    {
        get => previewMetaVisibility;
        set
        {
            previewMetaVisibility = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(ResultsSupportPanelVisibility));
            OnPropertyChanged(nameof(ResultsPreviewSummaryVisibility));
            OnPropertyChanged(nameof(ResultsRightColumnWidth));
        }
    }
    public bool CanOpenPreviewAttachment => previewCandidate is not null && HasLocalAttachment(previewCandidate);
    public bool CanDownloadPreviewAttachment => previewCandidate is not null
        && !previewCandidate.AttachmentName.Equals("[Email-Text]", StringComparison.OrdinalIgnoreCase)
        && !HasLocalAttachment(previewCandidate);

    private async Task ShowPreviewAsync(DocumentCandidate candidate, bool updateStatus = true)
    {
        var currentVersion = ++previewLoadVersion;
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
        PreviewMetaVisibility = Visibility.Visible;
        PreviewContentText = $@"Mail-Inhalt wird geladen...

Betreff: {candidate.Subject}
Absender: {candidate.Sender}
Konto: {candidate.AccountName}
Ordner: {candidate.FolderName}
Empfangen: {candidate.ReceivedAt.LocalDateTime:dd.MM.yyyy HH:mm:ss}
Anhang: {candidate.AttachmentName} ({candidate.AttachmentSizeInBytes} Bytes)

Status: {candidate.Status}
Kategorie: {candidate.SuggestedCategory}
Datei: {CandidateListItem.FromCandidate(candidate).FileAvailabilityLabel}";

        PreviewTabVisibility = System.Windows.Visibility.Visible;
        SetCurrentPage(WorkspacePage.Results);
        OnPropertyChanged(nameof(CanOpenPreviewAttachment));
        OnPropertyChanged(nameof(CanDownloadPreviewAttachment));
        if (updateStatus)
        {
            StatusMessage = $"Vorschau geladen: {candidate.Subject}";
        }

        try
        {
            var previewText = await mailPreviewService.GetPlainTextPreviewAsync(candidate);
            if (currentVersion == previewLoadVersion && previewCandidate?.Id == candidate.Id)
            {
                PreviewContentText = previewText;
            }
        }
        catch (Exception ex)
        {
            if (currentVersion == previewLoadVersion && previewCandidate?.Id == candidate.Id)
            {
                PreviewContentText = $@"Mail-Vorschau konnte nicht geladen werden.

Betreff: {candidate.Subject}
Absender: {candidate.Sender}
Konto: {candidate.AccountName}
Ordner: {candidate.FolderName}

Fehler: {ex.Message}";
            }
        }
    }

    private void OnOpenPreviewAttachmentClicked(object sender, System.Windows.RoutedEventArgs e)
    {
        if (previewCandidate is null)
        {
            return;
        }

        OpenCandidateAttachment(previewCandidate);
    }

    private void OnRevealPreviewAttachmentClicked(object sender, System.Windows.RoutedEventArgs e)
    {
        if (previewCandidate is null)
        {
            return;
        }

        RevealCandidateAttachment(previewCandidate);
    }

    private void OpenCandidateAttachment(DocumentCandidate candidate)
    {
        try
        {
            if (candidate.AttachmentName.Equals("[Email-Text]", StringComparison.OrdinalIgnoreCase))
            {
                _ = ShowPreviewAsync(candidate);
                return;
            }

            var foundPath = ResolveCandidateAttachmentPath(candidate);
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

    private void RevealCandidateAttachment(DocumentCandidate candidate)
    {
        try
        {
            var foundPath = ResolveCandidateAttachmentPath(candidate);
            if (foundPath != null)
            {
                System.Diagnostics.Process.Start("explorer.exe", $"/select,\"{foundPath}\"");
                StatusMessage = $"Datei im Ordner markiert: {candidate.AttachmentName}";
                return;
            }

            StatusMessage = $"Dokument nicht gefunden: {candidate.AttachmentName}. Bitte zuerst herunterladen.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Fehler beim Oeffnen des Zielordners: {ex.Message}";
        }
    }

    private static string? ResolveCandidateAttachmentPath(DocumentCandidate candidate)
    {
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

        return possiblePaths.FirstOrDefault(path => !string.IsNullOrEmpty(path) && System.IO.File.Exists(path));
    }

    private static bool HasLocalAttachment(DocumentCandidate candidate)
    {
        return ResolveCandidateAttachmentPath(candidate) is not null;
    }
}
