using InvoiceGenerator.Models;
using Microsoft.EntityFrameworkCore;

namespace InvoiceGenerator.Data;

public static class DbInitializer
{
    public static void Initialize(AppDbContext db)
    {
        ConfigureSqliteForSharedDrive(db);

        db.Database.Migrate();

        ConfigureSqliteForSharedDrive(db);

        AddCounterIfMissing(db, "Invoice", "INV");
        AddCounterIfMissing(db, "Quote", "QTE");
        UseSixDigitCounters(db);

        db.SaveChanges();
    }

    private static void AddCounterIfMissing(
        AppDbContext db,
        string documentType,
        string prefix)
    {
        bool exists = db.DocumentCounters.Any(
            counter => counter.DocumentType == documentType
        );

        if (exists)
        {
            return;
        }

        db.DocumentCounters.Add(new DocumentCounter
        {
            DocumentType = documentType,
            Prefix = prefix,
            CurrentNumber = 0,
            NumberLength = 6
        });
    }

    private static void UseSixDigitCounters(AppDbContext db)
    {
        foreach (DocumentCounter counter in db.DocumentCounters)
        {
            counter.NumberLength = 6;
        }
    }

    private static void ConfigureSqliteForSharedDrive(AppDbContext db)
    {
        db.Database.OpenConnection();

        try
        {
            // Do not change journal_mode during startup. Changing the journal mode
            // requires an exclusive lock and is unreliable on SMB/GVFS-mounted
            // folders. The database keeps the mode it was created with.
            db.Database.ExecuteSqlRaw(
                "PRAGMA busy_timeout=30000;"
            );
        }
        finally
        {
            db.Database.CloseConnection();
        }
    }
}
