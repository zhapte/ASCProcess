using InvoiceGenerator.Services;
using InvoiceGenerator.Models;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace InvoiceGenerator.Data;

public class AppDbContext : DbContext
{
    public DbSet<DocumentRecord> Documents => Set<DocumentRecord>();

    public DbSet<InvoiceItem> InvoiceItems => Set<InvoiceItem>();

    public DbSet<DocumentCounter> DocumentCounters =>
        Set<DocumentCounter>();

    protected override void OnConfiguring(
        DbContextOptionsBuilder optionsBuilder)
    {
        AppSettings settings =
            AppSettingsService.Load();

        string databaseFolder =
            AppSettingsService.ResolvePath(
                settings.Storage.DatabaseFolder
            );

        Directory.CreateDirectory(databaseFolder);

        string databasePath = Path.Combine(
            databaseFolder,
            "invoice.db"
        );

        SqliteConnectionStringBuilder connectionStringBuilder = new()
        {
            DataSource = databasePath,
            DefaultTimeout = 30,
            Pooling = false
        };

        optionsBuilder.UseSqlite(
            connectionStringBuilder.ToString()
        );
    }

    protected override void OnModelCreating(
        ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<DocumentRecord>()
            .HasIndex(document => document.DocumentNumber)
            .IsUnique()
            .HasFilter("DocumentNumber IS NOT NULL");

        modelBuilder.Entity<DocumentCounter>()
            .HasIndex(counter => counter.DocumentType)
            .IsUnique();

        modelBuilder.Entity<DocumentRecord>()
            .HasMany(document => document.Items)
            .WithOne(item => item.DocumentRecord)
            .HasForeignKey(item => item.DocumentRecordId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<DocumentRecord>()
            .Property(document => document.Subtotal)
            .HasPrecision(18, 2);

        modelBuilder.Entity<DocumentRecord>()
            .Property(document => document.Gst)
            .HasPrecision(18, 2);

        modelBuilder.Entity<DocumentRecord>()
            .Property(document => document.Pst)
            .HasPrecision(18, 2);

        modelBuilder.Entity<DocumentRecord>()
            .Property(document => document.Total)
            .HasPrecision(18, 2);

        modelBuilder.Entity<InvoiceItem>()
            .Property(item => item.Quantity)
            .HasPrecision(18, 2);

        modelBuilder.Entity<InvoiceItem>()
            .Property(item => item.UnitPrice)
            .HasPrecision(18, 2);

        modelBuilder.Entity<InvoiceItem>()
            .Property(item => item.Discount)
            .HasPrecision(18, 2);

        modelBuilder.Entity<InvoiceItem>()
            .Property(item => item.LineTotal)
            .HasPrecision(18, 2);
    }
}
