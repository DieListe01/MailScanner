using Microsoft.EntityFrameworkCore;

namespace MailScanner.Data;

public static class DatabaseInitializer
{
    public static async Task InitializeAsync(MailScannerDbContext dbContext, CancellationToken cancellationToken = default)
    {
        await dbContext.Database.EnsureCreatedAsync(cancellationToken);
        await EnsureDocumentCandidateAttachmentIndexAsync(dbContext, cancellationToken);
    }

    private static async Task EnsureDocumentCandidateAttachmentIndexAsync(MailScannerDbContext dbContext, CancellationToken cancellationToken)
    {
        await using var connection = dbContext.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open)
        {
            await connection.OpenAsync(cancellationToken);
        }

        var hasAttachmentIndex = false;
        await using (var columnCommand = connection.CreateCommand())
        {
            columnCommand.CommandText = "PRAGMA table_info(DocumentCandidates);";
            await using var reader = await columnCommand.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                if (string.Equals(reader[1]?.ToString(), "AttachmentIndex", StringComparison.OrdinalIgnoreCase))
                {
                    hasAttachmentIndex = true;
                    break;
                }
            }
        }

        if (!hasAttachmentIndex)
        {
            await using var addColumnCommand = connection.CreateCommand();
            addColumnCommand.CommandText = "ALTER TABLE DocumentCandidates ADD COLUMN AttachmentIndex INTEGER NOT NULL DEFAULT -1;";
            await addColumnCommand.ExecuteNonQueryAsync(cancellationToken);
        }

        await using (var dropIndexCommand = connection.CreateCommand())
        {
            dropIndexCommand.CommandText = "DROP INDEX IF EXISTS IX_DocumentCandidates_AccountAddress_FolderName_ImapUid_AttachmentName;";
            await dropIndexCommand.ExecuteNonQueryAsync(cancellationToken);
        }

        await using var createIndexCommand = connection.CreateCommand();
        createIndexCommand.CommandText = "CREATE UNIQUE INDEX IF NOT EXISTS IX_DocumentCandidates_AccountAddress_FolderName_ImapUid_AttachmentIndex_AttachmentName ON DocumentCandidates (AccountAddress, FolderName, ImapUid, AttachmentIndex, AttachmentName);";
        await createIndexCommand.ExecuteNonQueryAsync(cancellationToken);
    }
}
