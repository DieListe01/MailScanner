using System.Security.Cryptography;
using MailKit.Net.Imap;
using MailScanner.Core.Configuration;
using MailScanner.Core.Enums;
using MailScanner.Core.Models;
using MailScanner.Core.Services;
using MimeKit;

namespace MailScanner.Infrastructure.Services;

public sealed class ImapDocumentDownloadService(
    IAppSettingsProvider settingsProvider,
    IDocumentCandidateStore documentCandidateStore,
    IDocumentRecordStore documentRecordStore) : IDocumentDownloadService
{
    private const int ImapTimeoutMilliseconds = 15000;

    public async Task<DownloadDocumentsResult> DownloadAsync(IEnumerable<DocumentCandidate> candidates, CancellationToken cancellationToken = default)
    {
        var downloaded = new List<DocumentRecord>();
        var updatedCandidates = new List<DocumentCandidate>();
        var errors = new List<string>();

        foreach (var candidate in candidates.Where(x => x.Status != DocumentCandidateStatus.Downloaded))
        {
            try
            {
                var document = await DownloadCandidateAsync(candidate, cancellationToken);
                downloaded.Add(document);
                updatedCandidates.Add(candidate.WithStatus(DocumentCandidateStatus.Downloaded, document.FilePath));
            }
            catch (Exception ex)
            {
                updatedCandidates.Add(candidate.WithStatus(DocumentCandidateStatus.Failed, candidate.StoredFilePath));
                errors.Add($"{candidate.AttachmentName}: {ex.Message}");
            }
        }

        if (downloaded.Count > 0)
        {
            await documentRecordStore.SaveAsync(downloaded, cancellationToken);
        }

        if (updatedCandidates.Count > 0)
        {
            await documentCandidateStore.UpsertAsync(updatedCandidates, cancellationToken);
        }

        return new DownloadDocumentsResult
        {
            DownloadedDocuments = downloaded,
            Errors = errors
        };
    }

    private async Task<DocumentRecord> DownloadCandidateAsync(DocumentCandidate candidate, CancellationToken cancellationToken)
    {
        var settings = settingsProvider.GetCurrentSettings();
        var account = settings.MailImport.Accounts.FirstOrDefault(x => x.EmailAddress.Equals(candidate.AccountAddress, StringComparison.OrdinalIgnoreCase));

        if (account is null)
        {
            throw new InvalidOperationException($"Kein IMAP-Konto fuer {candidate.AccountAddress} konfiguriert.");
        }

        using var client = new ImapClient();
        client.Timeout = ImapTimeoutMilliseconds;
        await client.ConnectAsync(account.ImapHost, account.ImapPort, account.UseSsl, cancellationToken);
        await client.AuthenticateAsync(account.UserName, account.Password, cancellationToken);

        var folder = await client.GetFolderAsync(candidate.FolderName, cancellationToken);
        await folder.OpenAsync(MailKit.FolderAccess.ReadOnly, cancellationToken);

        var message = await folder.GetMessageAsync(new MailKit.UniqueId(candidate.ImapUid), cancellationToken);
        var attachment = message.Attachments
            .OfType<MimePart>()
            .FirstOrDefault(x => string.Equals(x.FileName, candidate.AttachmentName, StringComparison.OrdinalIgnoreCase) && IsPdfAttachment(x));

        if (attachment is null)
        {
            throw new InvalidOperationException("PDF-Anhang wurde in der Mail nicht gefunden.");
        }

        if (attachment.Content is null)
        {
            throw new InvalidOperationException("PDF-Anhang enthaelt keinen lesbaren Inhalt.");
        }

        var targetPath = BuildTargetPath(candidate, settings.Storage);
        Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);

        await using (var fileStream = File.Create(targetPath))
        {
            await attachment.Content.DecodeToAsync(fileStream, cancellationToken);
        }

        var fileHash = await ComputeSha256Async(targetPath, cancellationToken);
        await client.DisconnectAsync(true, cancellationToken);

        return new DocumentRecord
        {
            CandidateId = candidate.Id,
            FilePath = targetPath,
            FileHash = fileHash,
            OriginalFileName = candidate.AttachmentName,
            Sender = candidate.Sender,
            DownloadedAt = DateTimeOffset.UtcNow,
            Category = candidate.SuggestedCategory
        };
    }

    private string BuildTargetPath(DocumentCandidate candidate, StorageSettings storageSettings)
    {
        var year = candidate.ReceivedAt.Year.ToString();
        var category = candidate.SuggestedCategory.ToString();
        var sender = SanitizeSegment(candidate.Sender);
        var fileName = SanitizeFileName(candidate.AttachmentName);
        var directory = Path.Combine(storageSettings.DocumentRootPath, year, category, sender);

        Directory.CreateDirectory(directory);

        var path = Path.Combine(directory, fileName);
        if (!File.Exists(path))
        {
            return path;
        }

        var suffix = $"_{candidate.ImapUid}";
        return Path.Combine(directory, $"{Path.GetFileNameWithoutExtension(fileName)}{suffix}{Path.GetExtension(fileName)}");
    }

    private static async Task<string> ComputeSha256Async(string filePath, CancellationToken cancellationToken)
    {
        await using var stream = File.OpenRead(filePath);
        using var sha = SHA256.Create();
        var hash = await sha.ComputeHashAsync(stream, cancellationToken);
        return Convert.ToHexString(hash);
    }

    private static string SanitizeSegment(string value)
    {
        var invalidChars = Path.GetInvalidFileNameChars();
        return string.Concat(value.Select(ch => invalidChars.Contains(ch) ? '_' : ch)).Trim().Trim('.');
    }

    private static string SanitizeFileName(string value)
    {
        var fallback = string.IsNullOrWhiteSpace(value) ? "document.pdf" : value;
        return SanitizeSegment(fallback);
    }

    private static bool IsPdfAttachment(MimePart part)
    {
        var fileName = part.FileName ?? string.Empty;
        var mediaType = part.ContentType?.MimeType ?? string.Empty;

        return fileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase)
            || mediaType.Equals("application/pdf", StringComparison.OrdinalIgnoreCase);
    }
}

file static class DocumentCandidateExtensions
{
    public static DocumentCandidate WithStatus(this DocumentCandidate candidate, DocumentCandidateStatus status, string storedFilePath)
    {
        return new DocumentCandidate
        {
            Id = candidate.Id,
            AccountName = candidate.AccountName,
            AccountAddress = candidate.AccountAddress,
            FolderName = candidate.FolderName,
            ImapUid = candidate.ImapUid,
            MessageId = candidate.MessageId,
            Sender = candidate.Sender,
            Subject = candidate.Subject,
            ReceivedAt = candidate.ReceivedAt,
            AttachmentName = candidate.AttachmentName,
            AttachmentSizeInBytes = candidate.AttachmentSizeInBytes,
            SuggestedCategory = candidate.SuggestedCategory,
            Status = status,
            StoredFilePath = storedFilePath
        };
    }
}
