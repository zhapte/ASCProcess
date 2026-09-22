using InvoiceGenerator.Data;
using InvoiceGenerator.Models;

namespace InvoiceGenerator.Services;

public class DocumentNumberService
{
    private const int DocumentNumberLength = 6;

    private readonly AppDbContext _db;

    public DocumentNumberService(AppDbContext db)
    {
        _db = db;
    }

    public string AssignNextNumber(DocumentRecord document)
    {
        DocumentCounter counter =
            _db.DocumentCounters.SingleOrDefault(
                item => item.DocumentType == document.DocumentType
            )
            ?? throw new InvalidOperationException(
                $"Counter '{document.DocumentType}' was not found."
            );

        counter.CurrentNumber++;

        string documentNumber =
            counter.CurrentNumber.ToString(
                $"D{DocumentNumberLength}"
            );

        document.DocumentNumber = documentNumber;

        return documentNumber;
    }
}
