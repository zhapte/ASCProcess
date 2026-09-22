namespace InvoiceGenerator.Models;

public class DocumentRecord
{
    public int Id { get; set; }

    public string? DocumentNumber { get; set; }

    // Invoice or Quote
    public string DocumentType { get; set; } = string.Empty;

    public string InvoiceVariant { get; set; } = "Regular";

    public DateTime CreatedDate { get; set; } = DateTime.Now;

    public string CustomerName { get; set; } = string.Empty;

    public string YearAndMake { get; set; } = string.Empty;

    public string Vin { get; set; } = string.Empty;

    public string StockNumber { get; set; } = string.Empty;

    public string PurchaseOrder { get; set; } = string.Empty;

    public string ClaimNumber { get; set; } = string.Empty;

    public string CalibrationLocation { get; set; } = string.Empty;

    public string CalibrationType { get; set; } = string.Empty;

    public decimal Subtotal { get; set; }

    public decimal Gst { get; set; }

    public decimal Pst { get; set; }

    public decimal Total { get; set; }

    public string Status { get; set; } = DocumentStatus.Draft;

    public string? WordFilePath { get; set; }

    public string? PdfFilePath { get; set; }

    public List<InvoiceItem> Items { get; set; } = new();
}
