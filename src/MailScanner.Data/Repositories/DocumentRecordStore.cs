using MailScanner.Core.Enums;
using MailScanner.Core.Models;
using MailScanner.Core.Services;
using MailScanner.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace MailScanner.Data.Repositories;

public sealed class DocumentRecordStore(MailScannerDbContext dbContext) : IDocumentRecordStore
{
    public async Task SaveAsync(IEnumerable<DocumentRecord> documents, CancellationToken cancellationToken = default)
    {
        foreach (var document in documents)
        {
            var existing = await dbContext.DocumentRecords.FirstOrDefaultAsync(x => x.CandidateId == document.CandidateId, cancellationToken);

            if (existing is null)
            {
                dbContext.DocumentRecords.Add(ToEntity(document));
                continue;
            }

            existing.FilePath = document.FilePath;
            existing.FileHash = document.FileHash;
            existing.OriginalFileName = document.OriginalFileName;
            existing.Sender = document.Sender;
            existing.InvoiceNumber = document.InvoiceNumber;
            existing.Amount = document.Amount;
            existing.DocumentDate = document.DocumentDate;
            existing.DownloadedAt = document.DownloadedAt;
            existing.Category = (int)document.Category;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyCollection<DocumentRecord>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var entities = await dbContext.DocumentRecords
            .ToListAsync(cancellationToken);

        return entities
            .OrderByDescending(x => x.DownloadedAt)
            .Select(ToModel)
            .ToArray();
    }

    private static DocumentRecordEntity ToEntity(DocumentRecord document)
    {
        return new DocumentRecordEntity
        {
            Id = document.Id,
            CandidateId = document.CandidateId,
            FilePath = document.FilePath,
            FileHash = document.FileHash,
            OriginalFileName = document.OriginalFileName,
            Sender = document.Sender,
            InvoiceNumber = document.InvoiceNumber,
            Amount = document.Amount,
            DocumentDate = document.DocumentDate,
            DownloadedAt = document.DownloadedAt,
            Category = (int)document.Category
        };
    }

    private static DocumentRecord ToModel(DocumentRecordEntity entity)
    {
        return new DocumentRecord
        {
            Id = entity.Id,
            CandidateId = entity.CandidateId,
            FilePath = entity.FilePath,
            FileHash = entity.FileHash,
            OriginalFileName = entity.OriginalFileName,
            Sender = entity.Sender,
            InvoiceNumber = entity.InvoiceNumber,
            Amount = entity.Amount,
            DocumentDate = entity.DocumentDate,
            DownloadedAt = entity.DownloadedAt,
            Category = (DocumentCategory)entity.Category
        };
    }
}
