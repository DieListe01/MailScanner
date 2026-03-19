using MailKit;
using MailKit.Net.Imap;
using MailKit.Search;
using MailScanner.Core.Configuration;
using MailScanner.Core.Enums;
using MailScanner.Core.Models;
using MailScanner.Core.Services;
using MimeKit;
using System.IO;

namespace MailScanner.Infrastructure.Services;

public sealed class ImapMailImportService(
    IAppSettingsProvider settingsProvider,
    IMailboxScanStateStore mailboxScanStateStore,
    IDocumentCandidateStore documentCandidateStore,
    ScanLogger logger) : IMailImportService
{
    private const int ImapTimeoutMilliseconds = 15000;
    private static readonly string[] InvoiceKeywords =
    [
        "invoice",
        "rechnung", 
        "bill",
        "beleg",
        "quittung",
        "abrechnung",
        "dokument",
        "document",
        "pdf",
        "anhang",
        "attachment",
        "bericht",
        "report",
        "bestellung",
        "order",
        "lieferschein",
        "delivery",
        "kalkulation",
        "angebot",
        "quote",
        "vertrag",
        "contract"
    ];

    public async Task<IReadOnlyCollection<DocumentCandidate>> ImportNewCandidatesAsync(IProgress<MailImportProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        var settings = settingsProvider.GetCurrentSettings().MailImport;
        var isFullScan = settings.InitialLookbackDays <= 0;
        var excludedFolderPatterns = settings.ExcludedFolderPatterns
            .Where(pattern => !string.IsNullOrWhiteSpace(pattern))
            .Select(pattern => pattern.Trim())
            .ToArray();
        var importedCandidates = new List<DocumentCandidate>();
        var accounts = settings.Accounts
            .Where(account => !string.IsNullOrWhiteSpace(account.EmailAddress)
                && !string.IsNullOrWhiteSpace(account.UserName)
                && !string.IsNullOrWhiteSpace(account.Password)
                && !string.IsNullOrWhiteSpace(account.ImapHost))
            .ToArray();

        var accountsCompleted = 0;
        var totalMessagesScanned = 0;
        var totalAttachmentMessagesFound = 0;
        var totalPdfCandidatesFound = 0;
        var totalInvoiceMatchesFound = 0;

        // Log scan start
        logger.LogInfo($"=== MAIL-SCAN START ===");
        logger.LogInfo($"Konten: {accounts.Length}, Voll-Scan: {isFullScan}, Lookback: {settings.InitialLookbackDays} Tage");
        logger.LogInfo($"Ausschluss-Ordner: {string.Join(", ", excludedFolderPatterns)}");

        foreach (var account in accounts)
        {
            logger.LogInfo($"--- SCAN KONTO: {account.DisplayName} ({account.EmailAddress}) ---");
            
            var accountExcludedFolderPatterns = excludedFolderPatterns
                .Concat(account.ExcludedFolderPatterns.Where(pattern => !string.IsNullOrWhiteSpace(pattern)).Select(pattern => pattern.Trim()))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

            var accountImportResult = await ImportAccountAsync(
                account,
                settings,
                accounts.Length,
                accountsCompleted,
                totalMessagesScanned,
                totalAttachmentMessagesFound,
                totalPdfCandidatesFound,
                totalInvoiceMatchesFound,
                isFullScan,
                accountExcludedFolderPatterns,
                progress,
                logger,
                cancellationToken);

            importedCandidates.AddRange(accountImportResult.Candidates);
            accountsCompleted++;
            totalMessagesScanned += accountImportResult.MessagesScanned;
            totalAttachmentMessagesFound += accountImportResult.AttachmentMessagesFound;
            totalPdfCandidatesFound += accountImportResult.PdfCandidatesFound;
            totalInvoiceMatchesFound += accountImportResult.InvoiceMatchesFound;
            
            logger.LogInfo($"Konto {account.DisplayName} abgeschlossen: {accountImportResult.MessagesScanned} Mails, {accountImportResult.AttachmentMessagesFound} mit Anhang, {accountImportResult.Candidates.Count} Treffer");
        }

        logger.LogInfo($"=== SCAN GESAMTERGEBNIS ===");
        logger.LogInfo($"Gesamt: {totalMessagesScanned} Mails gescannt, {totalAttachmentMessagesFound} mit Anhang, {importedCandidates.Count} Treffer");
        logger.LogInfo($"PDFs: {totalPdfCandidatesFound}, Rechnungen: {totalInvoiceMatchesFound}");
        await logger.SaveLogAsync();

        if (importedCandidates.Count > 0)
        {
            await documentCandidateStore.UpsertAsync(importedCandidates, cancellationToken);
        }

        return await documentCandidateStore.GetAllAsync(cancellationToken);
    }

    private async Task<AccountImportResult> ImportAccountAsync(
        ImapAccountSettings account,
        MailImportSettings settings,
        int accountsTotal,
        int accountsCompleted,
        int baseMessagesScanned,
        int baseAttachmentMessagesFound,
        int basePdfCandidatesFound,
        int baseInvoiceMatchesFound,
        bool isFullScan,
        IReadOnlyCollection<string> excludedFolderPatterns,
        IProgress<MailImportProgress>? progress,
        ScanLogger logger,
        CancellationToken cancellationToken)
    {
        using var client = new ImapClient();
        client.Timeout = ImapTimeoutMilliseconds;

        await client.ConnectAsync(account.ImapHost, account.ImapPort, account.UseSsl, cancellationToken);
        await client.AuthenticateAsync(account.UserName, account.Password, cancellationToken);

        var rootFolder = await client.GetFolderAsync(account.FolderName, cancellationToken);
        var discoveredFolders = await GetFolderTreeAsync(rootFolder, cancellationToken);
        var allFolders = discoveredFolders
            .Where(folder => !IsExcludedFolder(folder.FullName, excludedFolderPatterns))
            .ToList();
        var excludedFolderCount = discoveredFolders.Count - allFolders.Count;
        var candidates = new List<DocumentCandidate>();
        var accountMessagesScanned = 0;
        var accountAttachmentMessagesFound = 0;
        var accountPdfCandidatesFound = 0;
        var accountInvoiceMatchesFound = 0;
        DateTimeOffset? oldestScannedMessageDate = null;

        for (var index = 0; index < allFolders.Count; index++)
        {
            var folder = allFolders[index];

            progress?.Report(new MailImportProgress
            {
                AccountName = account.DisplayName,
                FolderName = folder.FullName,
                AccountsTotal = accountsTotal,
                AccountsCompleted = accountsCompleted,
                FoldersTotal = allFolders.Count,
                FoldersCompleted = index,
                ConfiguredLookbackDays = settings.InitialLookbackDays,
                IsFullScan = isFullScan,
                MessagesScanned = baseMessagesScanned + accountMessagesScanned,
                AttachmentMessagesFound = baseAttachmentMessagesFound + accountAttachmentMessagesFound,
                PdfCandidatesFound = basePdfCandidatesFound + accountPdfCandidatesFound,
                InvoiceMatchesFound = baseInvoiceMatchesFound + accountInvoiceMatchesFound,
                OldestScannedMessageAgeDays = GetMessageAgeInDays(oldestScannedMessageDate),
                OldestScannedMessageDate = oldestScannedMessageDate,
                ExcludedFolderCount = excludedFolderCount,
                StatusText = $"Pruefe Ordner {index + 1} von {allFolders.Count}: {folder.FullName}"
            });

            if (folder.Attributes.HasFlag(FolderAttributes.NoSelect))
            {
                continue;
            }

            try
            {
                await folder.OpenAsync(FolderAccess.ReadOnly, cancellationToken);

                var lastState = await mailboxScanStateStore.GetAsync(account.EmailAddress, folder.FullName, cancellationToken);

                // Always use lookback scan for reliability - UID-based incremental scan can miss emails
                var lookbackDays = isFullScan ? 3650 : settings.InitialLookbackDays; // 10 years for "full" scan
                var searchDate = DateTime.Today.AddDays(-lookbackDays);
                
                IList<UniqueId> uniqueIds = await folder.SearchAsync(SearchQuery.DeliveredAfter(searchDate), cancellationToken);
                
                logger.LogInfo($"Suche in {folder.FullName}: {uniqueIds.Count} Mails seit {searchDate:dd.MM.yyyy}");

                uint maxUid = lastState?.LastScannedUid ?? 0;

                progress?.Report(new MailImportProgress
                {
                    AccountName = account.DisplayName,
                    FolderName = folder.FullName,
                    AccountsTotal = accountsTotal,
                    AccountsCompleted = accountsCompleted,
                    FoldersTotal = allFolders.Count,
                    FoldersCompleted = index,
                    ConfiguredLookbackDays = settings.InitialLookbackDays,
                    IsFullScan = isFullScan,
                    MessagesScanned = baseMessagesScanned + accountMessagesScanned,
                    AttachmentMessagesFound = baseAttachmentMessagesFound + accountAttachmentMessagesFound,
                    PdfCandidatesFound = basePdfCandidatesFound + accountPdfCandidatesFound,
                    InvoiceMatchesFound = baseInvoiceMatchesFound + accountInvoiceMatchesFound,
                    OldestScannedMessageAgeDays = GetMessageAgeInDays(oldestScannedMessageDate),
                    OldestScannedMessageDate = oldestScannedMessageDate,
                    ExcludedFolderCount = excludedFolderCount,
                    StatusText = $"Scanne {uniqueIds.Count} Mails in {folder.FullName}..."
                });

                foreach (var uniqueId in uniqueIds.OrderBy(x => x.Id))
                {
                    try
                    {
                        var message = await folder.GetMessageAsync(uniqueId, cancellationToken);
                        accountMessagesScanned++;
                        oldestScannedMessageDate = oldestScannedMessageDate is null || message.Date < oldestScannedMessageDate
                            ? message.Date
                            : oldestScannedMessageDate;
                        var hasAttachments = message.Attachments.Any();

                        if (hasAttachments)
                        {
                            accountAttachmentMessagesFound++;
                        }

                        var isInvoiceMatch = IsInvoiceMatch(message);
                        var sender = message.From.Mailboxes.FirstOrDefault()?.Address ?? "Unbekannt";
                        var subject = message.Subject ?? "Kein Betreff";

                        // Log EVERY mail with detailed info
                        logger.LogMail(account.DisplayName, folder.FullName, subject, sender, hasAttachments, false, isInvoiceMatch);

                        // Log attachment details
                        if (hasAttachments)
                        {
                            var attachmentNames = message.Attachments.OfType<MimePart>().Select(a => a.FileName ?? "no-name").ToArray();
                            logger.LogInfo($"Anhänge in Mail: {string.Join(", ", attachmentNames)}");
                        }

                        var pdfParts = message.Attachments
                            .OfType<MimePart>()
                            .Where(IsPdfAttachment)
                            .ToArray();

                        // Add ALL attachments, not just PDFs or invoices
                        var allAttachments = message.Attachments
                            .OfType<MimePart>()
                            .Where(part => !string.IsNullOrWhiteSpace(part.FileName))
                            .ToArray();

                        if (isInvoiceMatch && pdfParts.Length > 0)
                        {
                            accountInvoiceMatchesFound++;
                            logger.LogInfo($"RECHNUNGS-TREFFER: {subject} von {sender}");
                        }

                        // Add ALL attachments - apply account-specific file type filters
                        var filteredAttachments = ApplyFileTypeFilters(allAttachments, account);
                        
                        foreach (var attachment in filteredAttachments)
                        {
                            var isPdf = IsPdfAttachment(attachment);
                            candidates.Add(new DocumentCandidate
                            {
                                AccountName = account.DisplayName,
                                AccountAddress = account.EmailAddress,
                                FolderName = folder.FullName,
                                ImapUid = uniqueId.Id,
                                MessageId = message.MessageId ?? string.Empty,
                                Sender = sender,
                                Subject = subject,
                                ReceivedAt = message.Date,
                                AttachmentName = attachment.FileName ?? "attachment",
                                AttachmentSizeInBytes = GetAttachmentSize(attachment),
                                SuggestedCategory = isPdf ? SuggestCategory(message, attachment) : DocumentCategory.Other,
                                Status = DocumentCandidateStatus.Pending
                            });
                            
                            var attachmentType = isPdf ? "PDF" : "DOC";
                            logger.LogInfo($"{attachmentType}-Treffer: {attachment.FileName} von {sender}");
                        }

                        // Also add mails without attachments if they match invoice keywords
                        if (!hasAttachments && isInvoiceMatch)
                        {
                            candidates.Add(new DocumentCandidate
                            {
                                AccountName = account.DisplayName,
                                AccountAddress = account.EmailAddress,
                                FolderName = folder.FullName,
                                ImapUid = uniqueId.Id,
                                MessageId = message.MessageId ?? string.Empty,
                                Sender = sender,
                                Subject = subject,
                                ReceivedAt = message.Date,
                                AttachmentName = "[Email-Text]",
                                AttachmentSizeInBytes = message.TextBody?.Length ?? 0,
                                SuggestedCategory = DocumentCategory.Invoice,
                                Status = DocumentCandidateStatus.Pending
                            });
                            
                            logger.LogInfo($"Rechnungs-Mail (ohne Anhang): {subject} von {sender}");
                        }

                        // Update counters correctly
                        accountPdfCandidatesFound += pdfParts.Length;

                        progress?.Report(new MailImportProgress
                        {
                            AccountName = account.DisplayName,
                            FolderName = folder.FullName,
                            AccountsTotal = accountsTotal,
                            AccountsCompleted = accountsCompleted,
                            FoldersTotal = allFolders.Count,
                            FoldersCompleted = index,
                            ConfiguredLookbackDays = settings.InitialLookbackDays,
                            IsFullScan = isFullScan,
                            MessagesScanned = baseMessagesScanned + accountMessagesScanned,
                            AttachmentMessagesFound = baseAttachmentMessagesFound + accountAttachmentMessagesFound,
                            PdfCandidatesFound = basePdfCandidatesFound + accountPdfCandidatesFound,
                            InvoiceMatchesFound = baseInvoiceMatchesFound + accountInvoiceMatchesFound,
                            OldestScannedMessageAgeDays = GetMessageAgeInDays(oldestScannedMessageDate),
                            OldestScannedMessageDate = oldestScannedMessageDate,
                            ExcludedFolderCount = excludedFolderCount,
                            StatusText = $"{account.DisplayName}: {accountMessagesScanned} Mails, {accountAttachmentMessagesFound} mit Anhang, {candidates.Count} Treffer, {accountPdfCandidatesFound} PDFs, {accountInvoiceMatchesFound} Rechnungen"
                        });
                    }
                    catch (Exception ex)
                    {
                        logger.LogError($"Fehler bei Mail {uniqueId.Id}: {ex.Message}", ex);
                        // Skip problematic messages but continue scanning
                        continue;
                    }

                    if (uniqueId.Id > maxUid)
                    {
                        maxUid = uniqueId.Id;
                    }
                }

                await mailboxScanStateStore.SaveAsync(new MailboxScanState
                {
                    AccountAddress = account.EmailAddress,
                    FolderName = folder.FullName,
                    LastScannedUid = maxUid,
                    UpdatedAt = DateTimeOffset.UtcNow
                }, cancellationToken);

                await folder.CloseAsync(false, cancellationToken);
            }
            catch (Exception ex)
            {
                // Skip problematic folders but continue scanning
                logger.LogError($"Fehler bei Ordner {folder.FullName}: {ex.Message}", ex);
                continue;
            }
        }
        
        await client.DisconnectAsync(true, cancellationToken);

        return new AccountImportResult
        {
            Candidates = candidates,
            MessagesScanned = accountMessagesScanned,
            AttachmentMessagesFound = accountAttachmentMessagesFound,
            PdfCandidatesFound = accountPdfCandidatesFound,
            InvoiceMatchesFound = accountInvoiceMatchesFound
        };
    }

    private sealed class AccountImportResult
    {
        public IReadOnlyCollection<DocumentCandidate> Candidates { get; init; } = [];
        public int MessagesScanned { get; init; }
        public int AttachmentMessagesFound { get; init; }
        public int PdfCandidatesFound { get; init; }
        public int InvoiceMatchesFound { get; init; }
    }

    private static async Task<List<IMailFolder>> GetFolderTreeAsync(IMailFolder rootFolder, CancellationToken cancellationToken)
    {
        var folders = new List<IMailFolder> { rootFolder };
        var children = await rootFolder.GetSubfoldersAsync(false, cancellationToken);

        foreach (var child in children)
        {
            folders.AddRange(await GetFolderTreeAsync(child, cancellationToken));
        }

        return folders;
    }

    private static bool IsPdfAttachment(MimePart part)
    {
        var fileName = part.FileName ?? string.Empty;
        var mediaType = part.ContentType?.MimeType ?? string.Empty;

        return fileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase)
            || mediaType.Equals("application/pdf", StringComparison.OrdinalIgnoreCase);
    }

    private static long GetAttachmentSize(MimePart part)
    {
        return part.Content?.Stream?.Length ?? 0;
    }

    private static int? GetMessageAgeInDays(DateTimeOffset? oldestScannedMessageDate)
    {
        if (oldestScannedMessageDate is null)
        {
            return null;
        }

        return Math.Max(0, (DateTimeOffset.Now - oldestScannedMessageDate.Value).Days);
    }

    private static bool IsExcludedFolder(string folderName, IEnumerable<string> excludedFolderPatterns)
    {
        return excludedFolderPatterns.Any(pattern =>
            folderName.Contains(pattern, StringComparison.OrdinalIgnoreCase));
    }

    private static DocumentCategory SuggestCategory(MimeMessage message, MimePart attachment)
    {
        var haystack = string.Join(' ', new[]
        {
            message.Subject ?? string.Empty,
            attachment.FileName ?? string.Empty,
            message.From.ToString()
        }).ToLowerInvariant();

        if (ContainsAnyKeyword(haystack, InvoiceKeywords))
        {
            return DocumentCategory.Invoice;
        }

        if (haystack.Contains("versicherung"))
        {
            return DocumentCategory.Insurance;
        }

        if (haystack.Contains("bank") || haystack.Contains("konto"))
        {
            return DocumentCategory.Bank;
        }

        if (haystack.Contains("steuer") || haystack.Contains("tax"))
        {
            return DocumentCategory.Taxes;
        }

        return DocumentCategory.Unknown;
    }

    private static bool IsInvoiceMatch(MimeMessage message)
    {
        var haystack = string.Join(' ', new[]
        {
            message.Subject ?? string.Empty,
            message.From.ToString(),
            message.TextBody ?? string.Empty,
            message.HtmlBody ?? string.Empty,
            string.Join(' ', message.Attachments.OfType<MimePart>().Select(part => part.FileName ?? string.Empty))
        }).ToLowerInvariant();

        return ContainsAnyKeyword(haystack, InvoiceKeywords);
    }

    private static bool ContainsAnyKeyword(string haystack, IEnumerable<string> keywords)
    {
        return keywords.Any(haystack.Contains);
    }

    private static MimePart[] ApplyFileTypeFilters(MimePart[] attachments, ImapAccountSettings account)
    {
        var ignoredPatterns = account.IgnoredAttachmentNamePatterns
            .Where(pattern => !string.IsNullOrWhiteSpace(pattern))
            .Select(pattern => pattern.Trim())
            .ToArray();

        return attachments.Where(attachment => 
        {
            var fileName = (attachment.FileName ?? string.Empty).ToLowerInvariant();
            var extension = Path.GetExtension(fileName);

            if (ignoredPatterns.Any(pattern => fileName.Contains(pattern, StringComparison.OrdinalIgnoreCase)))
            {
                return false;
            }
            
            // PDF
            if (account.SearchPdf && extension == ".pdf")
                return true;
                
            // DOC
            if (account.SearchDoc && extension == ".doc")
                return true;
                
            // DOCX
            if (account.SearchDocx && extension == ".docx")
                return true;
                
            // XLSX
            if (account.SearchXlsx && extension == ".xlsx")
                return true;
                
            // XLS
            if (account.SearchXls && extension == ".xls")
                return true;
                
            // PPTX
            if (account.SearchPptx && extension == ".pptx")
                return true;
                
            // PPT
            if (account.SearchPpt && extension == ".ppt")
                return true;
                
            // Images
            if (account.SearchImages && IsImageFile(extension))
                return true;
                
            // TXT
            if (account.SearchTxt && extension == ".txt")
                return true;
                
            // Other
            if (account.SearchOther)
                return true;
                
            return false;
        }).ToArray();
    }

    private static bool IsImageFile(string extension)
    {
        var imageExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".tiff", ".webp" };
        return imageExtensions.Contains(extension);
    }
}
