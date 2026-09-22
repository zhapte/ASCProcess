using InvoiceGenerator.Data;
using InvoiceGenerator.Models;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;

namespace InvoiceGenerator.Services;

public sealed class InvoiceCreationService
{
    public DocumentRecord Generate(DocumentRecord draft)
    {
        // Keep the editable draft detached even when a transaction fails.
        var document = JsonSerializer.Deserialize<DocumentRecord>(JsonSerializer.Serialize(draft))!;
        InvoiceBuilder.Recalculate(document);
        using var db = new AppDbContext();
        DbInitializer.Initialize(db);
        using var transaction = db.Database.BeginTransaction();
        string? word = null;
        string? pdf = null;
        try
        {
            db.Documents.Add(document);
            db.SaveChanges();
            new DocumentNumberService(db).AssignNextNumber(document);
            word = new WordInvoiceService().Generate(document);
            document.WordFilePath = word;
            document.Status = DocumentStatus.Generated;
            var pdfService = new PdfService();
            if (pdfService.IsAvailable())
            {
                pdf = pdfService.ExportToPdf(word);
                document.PdfFilePath = pdf;
                document.Status = DocumentStatus.Completed;
                if (AppSettingsService.Load().Pdf.DeleteWordDocumentAfterPdf)
                {
                    File.Delete(word);
                    document.WordFilePath = null;
                    word = null;
                }
            }
            db.SaveChanges();
            transaction.Commit();
            return document;
        }
        catch
        {
            transaction.Rollback();
            foreach (string? path in new[] { pdf, word })
                if (path is not null) { try { File.Delete(path); } catch { } }
            throw;
        }
    }
}
