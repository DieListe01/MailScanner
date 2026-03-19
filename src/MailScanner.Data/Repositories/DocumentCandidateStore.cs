using MailScanner.Core.Enums;
using MailScanner.Core.Models;
using MailScanner.Core.Services;
using MailScanner.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace MailScanner.Data.Repositories;

public sealed class DocumentCandidateStore(MailScannerDbContext dbContext) : IDocumentCandidateStore
{
    public async Task UpsertAsync(IEnumerable<DocumentCandidate> candidates, CancellationToken cancellationToken = default)
    {
        foreach (var candidate in candidates)
        {
            var existing = await dbContext.DocumentCandidates.FirstOrDefaultAsync(
                x => x.AccountAddress == candidate.AccountAddress
                    && x.FolderName == candidate.FolderName
                    && x.ImapUid == candidate.ImapUid
                    && x.AttachmentName == candidate.AttachmentName,
                cancellationToken);

            if (existing is null)
            {
                dbContext.DocumentCandidates.Add(ToEntity(candidate));
                continue;
            }

            existing.AccountName = candidate.AccountName;
            existing.MessageId = candidate.MessageId;
            existing.Sender = candidate.Sender;
            existing.Subject = candidate.Subject;
            existing.ReceivedAt = candidate.ReceivedAt;
            existing.AttachmentSizeInBytes = candidate.AttachmentSizeInBytes;
            existing.SuggestedCategory = (int)candidate.SuggestedCategory;
            existing.Status = (int)candidate.Status;
            existing.StoredFilePath = candidate.StoredFilePath;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyCollection<DocumentCandidate>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var entities = await BuildQuery(null)
            .ToListAsync(cancellationToken);

        return entities
            .OrderByDescending(x => x.ReceivedAt)
            .ThenBy(x => x.AttachmentName)
            .Select(ToModel)
            .ToArray();
    }

    public async Task<IReadOnlyCollection<DocumentCandidate>> SearchAsync(string? searchText, CancellationToken cancellationToken = default)
    {
        var entities = await BuildQuery(searchText)
            .ToListAsync(cancellationToken);

        return entities
            .OrderByDescending(x => x.ReceivedAt)
            .ThenBy(x => x.AttachmentName)
            .Select(ToModel)
            .ToArray();
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await dbContext.DocumentCandidates.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (entity is not null)
        {
            dbContext.DocumentCandidates.Remove(entity);
            await dbContext.SaveChangesAsync(cancellationToken);
        }
    }

    private IQueryable<DocumentCandidateEntity> BuildQuery(string? searchText)
    {
        var query = dbContext.DocumentCandidates.AsNoTracking();

        if (string.IsNullOrWhiteSpace(searchText))
        {
            return query;
        }

        var pattern = $"%{searchText.Trim()}%";

        return query.Where(x =>
            EF.Functions.Like(x.AccountName, pattern)
            || EF.Functions.Like(x.AccountAddress, pattern)
            || EF.Functions.Like(x.Sender, pattern)
            || EF.Functions.Like(x.Subject, pattern)
            || EF.Functions.Like(x.AttachmentName, pattern)
            || EF.Functions.Like(x.StoredFilePath, pattern)
            || EF.Functions.Like(x.MessageId, pattern));
    }

    private static DocumentCandidateEntity ToEntity(DocumentCandidate candidate)
    {
        return new DocumentCandidateEntity
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
            SuggestedCategory = (int)candidate.SuggestedCategory,
            Status = (int)candidate.Status,
            StoredFilePath = candidate.StoredFilePath
        };
    }

    private static DocumentCandidate ToModel(DocumentCandidateEntity entity)
    {
        return new DocumentCandidate
        {
            Id = entity.Id,
            AccountName = entity.AccountName,
            AccountAddress = entity.AccountAddress,
            FolderName = entity.FolderName,
            ImapUid = entity.ImapUid,
            MessageId = entity.MessageId,
            Sender = entity.Sender,
            Subject = entity.Subject,
            ReceivedAt = entity.ReceivedAt,
            AttachmentName = entity.AttachmentName,
            AttachmentSizeInBytes = entity.AttachmentSizeInBytes,
            SuggestedCategory = (DocumentCategory)entity.SuggestedCategory,
            Status = (DocumentCandidateStatus)entity.Status,
            StoredFilePath = entity.StoredFilePath
        };
    }
}
