using MailScanner.Core.Models;
using MailScanner.Core.Services;
using MailScanner.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace MailScanner.Data.Repositories;

public sealed class MailboxScanStateStore(MailScannerDbContext dbContext) : IMailboxScanStateStore
{
    public async Task<MailboxScanState?> GetAsync(string accountAddress, string folderName, CancellationToken cancellationToken = default)
    {
        var entity = await dbContext.MailboxScanStates.FirstOrDefaultAsync(
            x => x.AccountAddress == accountAddress && x.FolderName == folderName,
            cancellationToken);

        return entity is null
            ? null
            : new MailboxScanState
            {
                AccountAddress = entity.AccountAddress,
                FolderName = entity.FolderName,
                LastScannedUid = entity.LastScannedUid,
                UpdatedAt = entity.UpdatedAt
            };
    }

    public async Task SaveAsync(MailboxScanState state, CancellationToken cancellationToken = default)
    {
        var entity = await dbContext.MailboxScanStates.FirstOrDefaultAsync(
            x => x.AccountAddress == state.AccountAddress && x.FolderName == state.FolderName,
            cancellationToken);

        if (entity is null)
        {
            dbContext.MailboxScanStates.Add(new MailboxScanStateEntity
            {
                AccountAddress = state.AccountAddress,
                FolderName = state.FolderName,
                LastScannedUid = state.LastScannedUid,
                UpdatedAt = state.UpdatedAt
            });
        }
        else
        {
            entity.LastScannedUid = state.LastScannedUid;
            entity.UpdatedAt = state.UpdatedAt;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
