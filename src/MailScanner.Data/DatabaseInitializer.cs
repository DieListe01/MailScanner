namespace MailScanner.Data;

public static class DatabaseInitializer
{
    public static async Task InitializeAsync(MailScannerDbContext dbContext, CancellationToken cancellationToken = default)
    {
        await dbContext.Database.EnsureCreatedAsync(cancellationToken);
    }
}
