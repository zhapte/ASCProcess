using InvoiceGenerator.Data;
using Microsoft.EntityFrameworkCore;

namespace InvoiceGenerator.Services;

public sealed class InvoiceOptionsService
{
    public Dictionary<string, int> ReadCounters()
    {
        using var db = new AppDbContext();
        return db.DocumentCounters.AsNoTracking().ToDictionary(c => c.DocumentType, c => c.CurrentNumber);
    }

    public void SetCounter(string type, int expected, int value)
    {
        if (type is not ("Invoice" or "Quote") || value < 0 || value >= 999999)
            throw new ArgumentException("Enter a last-used number from 0 to 999998.");
        using var db = new AppDbContext();
        using var transaction = db.Database.BeginTransaction();
        var counter = db.DocumentCounters.Single(c => c.DocumentType == type);
        if (counter.CurrentNumber != expected)
            throw new InvalidOperationException("Counter changed since loading. Refresh before saving.");
        if (value < expected)
            throw new InvalidOperationException("Lowering the counter could reuse invoice numbers and is not allowed here.");
        new CounterSettingsService(db).SetCurrentNumber(type, value);
        transaction.Commit();
    }

    public void SetLaborRate(decimal rate)
    {
        if (rate <= 0) throw new ArgumentException("Labor rate must be greater than zero.");
        var settings = AppSettingsService.Load();
        settings.Service.LaborRate = Math.Round(rate, 2, MidpointRounding.AwayFromZero);
        AppSettingsService.Save(settings);
    }
}
