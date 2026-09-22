using InvoiceGenerator.Data;
using InvoiceGenerator.Models;

namespace InvoiceGenerator.Services;

public class CounterSettingsService
{
    private readonly AppDbContext _db;

    public CounterSettingsService(AppDbContext db)
    {
        _db = db;
    }

    public void SetCurrentNumber(
        string documentType,
        int currentNumber)
    {
        if (currentNumber < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(currentNumber),
                "The current number cannot be negative."
            );
        }

        DocumentCounter counter =
            _db.DocumentCounters.SingleOrDefault(
                item => item.DocumentType == documentType
            )
            ?? throw new InvalidOperationException(
                $"Counter '{documentType}' was not found."
            );

        counter.CurrentNumber = currentNumber;

        _db.SaveChanges();
    }
}