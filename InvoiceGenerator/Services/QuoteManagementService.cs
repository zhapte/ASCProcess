using InvoiceGenerator.Data;
using InvoiceGenerator.Models;
using Microsoft.EntityFrameworkCore;

namespace InvoiceGenerator.Services;

public sealed class QuoteManagementService
{
    public IReadOnlyList<DocumentRecord> ListQuotes()
    {
        using var db = new AppDbContext();
        DbInitializer.Initialize(db);
        return db.Documents
            .AsNoTracking()
            .Include(document => document.Items)
            .Where(document => document.DocumentType == "Quote")
            .OrderByDescending(document => document.CreatedDate)
            .ToList();
    }

    public DocumentRecord ConvertToInvoice(int quoteId)
    {
        using var db = new AppDbContext();
        DbInitializer.Initialize(db);
        DocumentRecord quote = db.Documents
            .AsNoTracking()
            .Include(document => document.Items)
            .SingleOrDefault(document => document.Id == quoteId && document.DocumentType == "Quote")
            ?? throw new InvalidOperationException("Quote was not found.");

        DocumentRecord invoice = new()
        {
            DocumentType = "Invoice",
            InvoiceVariant = "Regular",
            CreatedDate = DateTime.Now,
            CustomerName = quote.CustomerName,
            YearAndMake = quote.YearAndMake,
            Vin = quote.Vin,
            StockNumber = quote.StockNumber,
            PurchaseOrder = quote.PurchaseOrder,
            ClaimNumber = quote.ClaimNumber,
            Subtotal = quote.Subtotal,
            Gst = quote.Gst,
            Pst = quote.Pst,
            Total = quote.Total,
            Status = DocumentStatus.Draft,
            Items = quote.Items.Select(item => new InvoiceItem
            {
                Quantity = item.Quantity,
                ItemNumber = item.ItemNumber,
                Description = item.Description,
                UnitPrice = item.UnitPrice,
                Discount = item.Discount,
                LineTotal = item.LineTotal
            }).ToList()
        };

        return new InvoiceCreationService().Generate(invoice);
    }

    public void RemoveQuote(int quoteId)
    {
        string? pdfFilePath;
        string? wordFilePath;
        using (var db = new AppDbContext())
        {
            DbInitializer.Initialize(db);
            DocumentRecord quote = db.Documents
                .Include(document => document.Items)
                .SingleOrDefault(document => document.Id == quoteId && document.DocumentType == "Quote")
                ?? throw new InvalidOperationException("Quote was not found.");

            pdfFilePath = quote.PdfFilePath;
            wordFilePath = quote.WordFilePath;
            db.Documents.Remove(quote);
            db.SaveChanges();
        }

        DeleteFileIfExists(pdfFilePath);
        DeleteFileIfExists(wordFilePath);
    }

    private static void DeleteFileIfExists(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return;
        File.Delete(path);
    }
}
