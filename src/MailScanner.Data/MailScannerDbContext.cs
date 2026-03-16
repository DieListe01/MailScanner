using MailScanner.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace MailScanner.Data;

public sealed class MailScannerDbContext(DbContextOptions<MailScannerDbContext> options) : DbContext(options)
{
    public DbSet<DocumentCandidateEntity> DocumentCandidates => Set<DocumentCandidateEntity>();
    public DbSet<DocumentRecordEntity> DocumentRecords => Set<DocumentRecordEntity>();
    public DbSet<MailboxScanStateEntity> MailboxScanStates => Set<MailboxScanStateEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<DocumentCandidateEntity>(entity =>
        {
            entity.ToTable("DocumentCandidates");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.AccountName).HasMaxLength(200);
            entity.Property(x => x.AccountAddress).HasMaxLength(320);
            entity.Property(x => x.FolderName).HasMaxLength(200);
            entity.Property(x => x.MessageId).HasMaxLength(500);
            entity.Property(x => x.Sender).HasMaxLength(500);
            entity.Property(x => x.Subject).HasMaxLength(500);
            entity.Property(x => x.AttachmentName).HasMaxLength(260);
            entity.Property(x => x.StoredFilePath).HasMaxLength(500);
            entity.HasIndex(x => new { x.AccountAddress, x.FolderName, x.ImapUid, x.AttachmentName }).IsUnique();
        });

        modelBuilder.Entity<DocumentRecordEntity>(entity =>
        {
            entity.ToTable("DocumentRecords");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.FilePath).HasMaxLength(500);
            entity.Property(x => x.FileHash).HasMaxLength(128);
            entity.Property(x => x.OriginalFileName).HasMaxLength(260);
            entity.Property(x => x.Sender).HasMaxLength(500);
            entity.Property(x => x.InvoiceNumber).HasMaxLength(100);
            entity.HasIndex(x => x.CandidateId).IsUnique();
            entity.HasIndex(x => x.FileHash);
        });

        modelBuilder.Entity<MailboxScanStateEntity>(entity =>
        {
            entity.ToTable("MailboxScanStates");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.AccountAddress).HasMaxLength(320);
            entity.Property(x => x.FolderName).HasMaxLength(200);
            entity.HasIndex(x => new { x.AccountAddress, x.FolderName }).IsUnique();
        });
    }
}
