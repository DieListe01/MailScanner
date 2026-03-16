using MailScanner.Core.Enums;
using MailScanner.Core.Models;

namespace MailScanner.App.Models;

public sealed class CandidateListItem
{
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
    public int PriorityScore { get; }
    public string PriorityLabel => PriorityScore >= 120 ? "Sehr hoch" : PriorityScore >= 80 ? "Hoch" : PriorityScore >= 45 ? "Mittel" : "Niedrig";
    public string MatchReason { get; }
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
        DocumentCandidateStatus.Downloaded => "#DBF3E5",
        DocumentCandidateStatus.Failed => "#FFE0E0",
        DocumentCandidateStatus.Ignored => "#ECEFF4",
        _ => "#E3EEFF"
    };
    public string StatusBadgeForeground => Candidate.Status switch
    {
        DocumentCandidateStatus.Downloaded => "#25673B",
        DocumentCandidateStatus.Failed => "#B54040",
        DocumentCandidateStatus.Ignored => "#58697A",
        _ => "#245D9B"
    };
    public string PriorityBadgeBackground => PriorityScore >= 120 ? "#CFE5FF" : PriorityScore >= 80 ? "#DBECFF" : PriorityScore >= 45 ? "#EAF3FF" : "#EDF2F7";
    public string PriorityBadgeForeground => PriorityScore >= 120 ? "#124D93" : PriorityScore >= 80 ? "#2669B6" : PriorityScore >= 45 ? "#3E6D98" : "#627284";

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
                reasons.Add("Kategorie Rechnung");
                break;
            case DocumentCategory.Taxes:
                score += 60;
                reasons.Add("Kategorie Steuern");
                break;
            case DocumentCategory.Insurance:
                score += 40;
                reasons.Add("Kategorie Versicherung");
                break;
            case DocumentCategory.Bank:
                score += 35;
                reasons.Add("Kategorie Bank");
                break;
        }

        if (ContainsAnyKeyword(subject, InvoiceKeywords))
        {
            score += 40;
            reasons.Add("Keyword im Betreff");
        }

        if (ContainsAnyKeyword(attachmentName, InvoiceKeywords))
        {
            score += 45;
            reasons.Add("Keyword im Dateinamen");
        }

        if (ContainsAnyKeyword(sender, ["rechnung", "invoice", "billing", "kundenservice", "service", "buchhaltung"]))
        {
            score += 18;
            reasons.Add("Auffaelliger Absender");
        }

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

        if (score == 0)
        {
            reasons.Add("Allgemeiner PDF-Kandidat");
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
