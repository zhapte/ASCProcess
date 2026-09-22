namespace InvoiceGenerator.Models;

public class InvoiceItem
{
    public int Id { get; set; }

    public int DocumentRecordId { get; set; }

    public decimal Quantity { get; set; }

    public string ItemNumber { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public decimal UnitPrice { get; set; }

    public decimal Discount { get; set; }

    public decimal LineTotal { get; set; }

    public DocumentRecord? DocumentRecord { get; set; }
}