using System.IO;
using MailScanner.Core.Enums;
using MailScanner.Core.Models;

namespace MailScanner.App.Models;

public sealed class CandidateListItem
{
    private static readonly string[] AttachmentKeywords =
    [
        "dokument", "document", "pdf", "anhang", "attachment",
        "bericht", "report", "bestellung", "order", "lieferschein",
        "delivery", "kalkulation", "angebot", "quote", "vertrag", "contract"
    ];

    private static readonly string[] InvoiceKeywords =
    [
        "invoice",
        "rechnung",
        "bill",
        "beleg",
        "quittung",
        "abrechnung"
    ];

    private CandidateListItem(DocumentCandidate candidate, int priorityScore, string matchReason)
    {
        Candidate = candidate;
        PriorityScore = priorityScore;
        MatchReason = matchReason;
    }

    public DocumentCandidate Candidate { get; }
    public string AccountName => Candidate.AccountName;
    public string Sender => Candidate.Sender;
    public string Subject => Candidate.Subject;
    public string AttachmentName => Candidate.AttachmentName;
    public string ReceivedAtDisplay => Candidate.ReceivedAt.LocalDateTime.ToString("dd.MM.yyyy HH:mm");
    public string AttachmentSizeDisplay => FormatFileSize(Candidate.AttachmentSizeInBytes);
    public string CategoryLabel => Candidate.SuggestedCategory.ToString();
    public string StatusLabel => Candidate.Status.ToString();
    public string FileAvailabilityLabel => Candidate.AttachmentName.Equals("[Email-Text]", StringComparison.OrdinalIgnoreCase)
        ? "Nur Vorschau"
        : Candidate.AlreadyDownloaded || (!string.IsNullOrWhiteSpace(Candidate.StoredFilePath) && File.Exists(Candidate.StoredFilePath))
            ? "Heruntergeladen"
            : "Datei fehlt";
    public bool IsInvoiceCandidate => Candidate.SuggestedCategory == DocumentCategory.Invoice;
    public int PriorityScore { get; }
    public string PriorityLabel => PriorityScore >= 120 ? "Sehr hoch" : PriorityScore >= 80 ? "Hoch" : PriorityScore >= 45 ? "Mittel" : "Niedrig";
    public string MatchReason { get; }
    public string MatchReasonCompact => MatchReason.Replace("Kategorie ", string.Empty).Replace("Keyword im ", "Keyword ");
    public string CategoryBadgeBackground => Candidate.SuggestedCategory switch
    {
        DocumentCategory.Invoice => "#D8EAFE",
        DocumentCategory.Taxes => "#DDEFD9",
        DocumentCategory.Insurance => "#E6E5FF",
        DocumentCategory.Bank => "#FFE6D5",
        _ => "#EAF0F7"
    };
    public string CategoryBadgeForeground => Candidate.SuggestedCategory switch
    {
        DocumentCategory.Invoice => "#16508D",
        DocumentCategory.Taxes => "#2D6C31",
        DocumentCategory.Insurance => "#5142A7",
        DocumentCategory.Bank => "#A25716",
        _ => "#4B617B"
    };
    public string StatusBadgeBackground => Candidate.Status switch
    {
        DocumentCandidateStatus.Downloaded => "#16261F",
        DocumentCandidateStatus.Failed => "#2B1820",
        DocumentCandidateStatus.Ignored => "#1B2230",
        _ => "#1A2233"
    };
    public string StatusBadgeForeground => Candidate.Status switch
    {
        DocumentCandidateStatus.Downloaded => "#7FD5A4",
        DocumentCandidateStatus.Failed => "#F2A3A3",
        DocumentCandidateStatus.Ignored => "#9BA9BB",
        _ => "#8FB8FF"
    };
    public string StatusBadgeLabel => FileAvailabilityLabel;
    public string PriorityBadgeBackground => PriorityScore >= 120 ? "#172338" : PriorityScore >= 80 ? "#1A2535" : PriorityScore >= 45 ? "#1D2430" : "#202733";
    public string PriorityBadgeForeground => PriorityScore >= 120 ? "#8CC2FF" : PriorityScore >= 80 ? "#9FC8FF" : PriorityScore >= 45 ? "#B3CAE6" : "#98A8BA";
    public string PriorityBadgeLabel => PriorityScore.ToString();

    public static CandidateListItem FromCandidate(DocumentCandidate candidate)
    {
        var reasons = new List<string>();
        var score = 0;
        var subject = candidate.Subject.ToLowerInvariant();
        var attachmentName = candidate.AttachmentName.ToLowerInvariant();
        var sender = candidate.Sender.ToLowerInvariant();

        switch (candidate.SuggestedCategory)
        {
            case DocumentCategory.Invoice:
                score += 80;
                reasons.Add("Rechnung");
                break;
            case DocumentCategory.Taxes:
                score += 60;
                reasons.Add("Steuer-Dokument");
                break;
            case DocumentCategory.Insurance:
                score += 40;
                reasons.Add("Versicherung");
                break;
            case DocumentCategory.Bank:
                score += 35;
                reasons.Add("Bank-Dokument");
                break;
            case DocumentCategory.Other:
                score += 20;
                reasons.Add("Allgemeines Dokument");
                break;
        }

        // Check for invoice keywords
        if (ContainsAnyKeyword(subject, InvoiceKeywords))
        {
            score += 40;
            reasons.Add("Rechnungs-Keyword");
        }

        if (ContainsAnyKeyword(attachmentName, InvoiceKeywords))
        {
            score += 45;
            reasons.Add("Rechnungs-Dateiname");
        }

        // Check for general document keywords
        if (ContainsAnyKeyword(subject, AttachmentKeywords))
        {
            score += 25;
            reasons.Add("Dokumenten-Keyword");
        }

        if (ContainsAnyKeyword(attachmentName, AttachmentKeywords))
        {
            score += 30;
            reasons.Add("Dokumenten-Dateiname");
        }

        // Check for suspicious senders
        if (ContainsAnyKeyword(sender, ["rechnung", "invoice", "billing", "kundenservice", "service", "buchhaltung"]))
        {
            score += 18;
            reasons.Add("Auffälliger Absender");
        }

        // File type bonus
        if (attachmentName.EndsWith(".pdf"))
        {
            score += 15;
            reasons.Add("PDF-Dokument");
        }
        else if (attachmentName.EndsWith(".doc") || attachmentName.EndsWith(".docx"))
        {
            score += 12;
            reasons.Add("Word-Dokument");
        }
        else if (attachmentName.EndsWith(".xls") || attachmentName.EndsWith(".xlsx"))
        {
            score += 12;
            reasons.Add("Excel-Dokument");
        }

        // Age-based scoring
        var ageInDays = Math.Max(0, (DateTimeOffset.Now - candidate.ReceivedAt).Days);

        if (ageInDays <= 14)
        {
            score += 20;
            reasons.Add("Sehr aktuell");
        }
        else if (ageInDays <= 60)
        {
            score += 12;
            reasons.Add("Aktuell");
        }

        if (candidate.Status == DocumentCandidateStatus.Pending)
        {
            score += 10;
        }

        // Fallback for very low scores
        if (score < 20)
        {
            reasons.Add("Mit Anhang");
            score = 20;
        }

        return new CandidateListItem(candidate, score, string.Join(" | ", reasons.Take(3)));
    }

    private static bool ContainsAnyKeyword(string haystack, IEnumerable<string> keywords)
    {
        return keywords.Any(keyword => haystack.Contains(keyword, StringComparison.OrdinalIgnoreCase));
    }

    private static string FormatFileSize(long bytes)
    {
        if (bytes >= 1024 * 1024)
        {
            return $"{bytes / 1024d / 1024d:0.0} MB";
        }

        if (bytes >= 1024)
        {
            return $"{bytes / 1024d:0} KB";
        }

        return $"{bytes} B";
    }
}
