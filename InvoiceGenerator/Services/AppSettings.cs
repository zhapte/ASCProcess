namespace InvoiceGenerator.Services;

public class AppSettings
{
    public StorageSettings Storage { get; set; } = new();

    public TemplateSettings Templates { get; set; } = new();

    public PdfSettings Pdf { get; set; } = new();

    public ServiceSettings Service { get; set; } = new();
}

public class StorageSettings
{
    public string DatabaseFolder { get; set; } = "Database";

    public string OutputFolder { get; set; } = "Output";
}

public class TemplateSettings
{
    public string InvoiceTemplatePath { get; set; } =
        "Templates/InvoiceTemplate_v1.docx";

    public string CalibrationTemplatePath { get; set; } =
        "Templates/Calibration_v2.docx";

    public string ServiceTemplatePath { get; set; } =
        "Templates/Service_v1.docx";
}

public class PdfSettings
{
    public bool DeleteWordDocumentAfterPdf { get; set; } = true;
}

public class ServiceSettings
{
    public decimal LaborRate { get; set; } = 124.48m;
}
