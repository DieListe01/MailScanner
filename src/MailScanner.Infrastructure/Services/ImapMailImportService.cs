using MailKit;
using MailKit.Net.Imap;
using MailKit.Search;
using MailScanner.Core.Configuration;
using MailScanner.Core.Enums;
using MailScanner.Core.Models;
using MailScanner.Core.Services;
using MimeKit;

namespace MailScanner.Infrastructure.Services;

public sealed class ImapMailImportService(
    IAppSettingsProvider settingsProvider,
    IMailboxScanStateStore mailboxScanStateStore,
    IDocumentCandidateStore documentCandidateStore) : IMailImportService
{
    private const int ImapTimeoutMilliseconds = 15000;
    private static readonly string[] InvoiceKeywords =
    [
        "invoice",
        "rechnung",
        "bill",
        "beleg",
        "quittung",
        "abrechnung"
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

        foreach (var account in accounts)
        {
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
                cancellationToken);

            importedCandidates.AddRange(accountImportResult.Candidates);
            accountsCompleted++;
            totalMessagesScanned += accountImportResult.MessagesScanned;
            totalAttachmentMessagesFound += accountImportResult.AttachmentMessagesFound;
            totalPdfCandidatesFound += accountImportResult.PdfCandidatesFound;
            totalInvoiceMatchesFound += accountImportResult.InvoiceMatchesFound;
        }

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

            await folder.OpenAsync(FolderAccess.ReadOnly, cancellationToken);

            var lastState = await mailboxScanStateStore.GetAsync(account.EmailAddress, folder.FullName, cancellationToken);

            IList<UniqueId> uniqueIds = isFullScan
                ? await folder.SearchAsync(SearchQuery.All, cancellationToken)
                : lastState is { LastScannedUid: > 0 }
                ? await folder.SearchAsync(SearchQuery.Uids(new UniqueIdRange(new UniqueId(lastState.LastScannedUid + 1), UniqueId.MaxValue)), cancellationToken)
                : await folder.SearchAsync(SearchQuery.DeliveredAfter(DateTime.Today.AddDays(-settings.InitialLookbackDays)), cancellationToken);

            uint maxUid = lastState?.LastScannedUid ?? 0;

            foreach (var uniqueId in uniqueIds.OrderBy(x => x.Id))
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

                var pdfParts = message.Attachments
                    .OfType<MimePart>()
                    .Where(IsPdfAttachment)
                    .ToArray();

                if (isInvoiceMatch && pdfParts.Length > 0)
                {
                    accountInvoiceMatchesFound++;
                }

                foreach (var attachment in pdfParts)
                {
                    candidates.Add(new DocumentCandidate
                    {
                        AccountName = account.DisplayName,
                        AccountAddress = account.EmailAddress,
                        FolderName = folder.FullName,
                        ImapUid = uniqueId.Id,
                        MessageId = message.MessageId ?? string.Empty,
                        Sender = message.From.Mailboxes.FirstOrDefault()?.Address ?? string.Empty,
                        Subject = message.Subject ?? string.Empty,
                        ReceivedAt = message.Date,
                        AttachmentName = attachment.FileName ?? "attachment.pdf",
                        AttachmentSizeInBytes = GetAttachmentSize(attachment),
                        SuggestedCategory = SuggestCategory(message, attachment),
                        Status = DocumentCandidateStatus.Pending
                    });

                    accountPdfCandidatesFound++;
                }

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
                    StatusText = $"{account.DisplayName}: {accountMessagesScanned} Mails durchsucht, {accountAttachmentMessagesFound} Mails mit Anhang, {accountPdfCandidatesFound} PDF-Anhaenge, {accountInvoiceMatchesFound} Rechnungs-Treffer"
                });

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
}
