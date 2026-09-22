using InvoiceGenerator.Data;
using InvoiceGenerator.Models;
using Microsoft.EntityFrameworkCore;

namespace InvoiceGenerator.Services;

public sealed record DocumentSearchResult(IReadOnlyList<DocumentRecord> Documents, bool HasMore);

public sealed class DocumentSearchService
{
    public const int ResultLimit = 100;

    public DocumentSearchResult Search(string searchText)
    {
        string term = searchText.Trim();
        if (term.Length == 0) throw new ArgumentException("Enter a search term.");
        // Treat SQL wildcard characters as literal user input.
        string pattern = "%" + term.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_") + "%";
        using var db = new AppDbContext();
        var documents = db.Documents.AsNoTracking().Include(d => d.Items)
            .Where(d =>
                (d.DocumentNumber != null && EF.Functions.Like(d.DocumentNumber, pattern, "\\")) ||
                EF.Functions.Like(d.CustomerName, pattern, "\\") ||
                EF.Functions.Like(d.DocumentType, pattern, "\\") ||
                EF.Functions.Like(d.InvoiceVariant, pattern, "\\") ||
                EF.Functions.Like(d.YearAndMake, pattern, "\\") ||
                EF.Functions.Like(d.Vin, pattern, "\\") ||
                EF.Functions.Like(d.StockNumber, pattern, "\\") ||
                EF.Functions.Like(d.PurchaseOrder, pattern, "\\") ||
                EF.Functions.Like(d.ClaimNumber, pattern, "\\") ||
                EF.Functions.Like(d.CalibrationLocation, pattern, "\\") ||
                EF.Functions.Like(d.CalibrationType, pattern, "\\"))
            .OrderByDescending(d => d.CreatedDate).ThenByDescending(d => d.Id)
            .Take(ResultLimit + 1).ToList();
        return new(documents.Take(ResultLimit).ToList(), documents.Count > ResultLimit);
    }
}
