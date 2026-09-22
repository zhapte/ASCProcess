namespace InvoiceGenerator.Models;

public class DocumentCounter
{
    public int Id { get; set; }

    public string DocumentType { get; set; } = string.Empty;

    public string Prefix { get; set; } = string.Empty;

    public int CurrentNumber { get; set; }

    public int NumberLength { get; set; } = 5;
}