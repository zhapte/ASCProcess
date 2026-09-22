using InvoiceGenerator.Data;
using InvoiceGenerator.Models;
using InvoiceGenerator.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

Console.Title = "Invoice Generator";

try
{
    using AppDbContext db = new();

    DbInitializer.Initialize(db);

    Console.WriteLine("Database initialized successfully.");
    Console.WriteLine();

    bool running = true;

    while (running)
    {
        string choice = ReadMainMenuChoice();

        switch (choice)
        {
            case "1":
                CreateInvoice(db);
                break;

            case "2":
                CreateQuote(db);
                break;

            case "3":
                SearchDocuments(db);
                break;

            case "4":
                ManageQuotes(db);
                break;

            case "5":
                ShowCounters(db);
                break;

            case "6":
                ConfigureCounters(db);
                break;

            case "7":
                ConfigureServiceLaborRate();
                break;

            case "8":
                running = false;
                break;

            default:
                Console.WriteLine(
                    "Please enter a number from 1 to 8."
                );
                break;
        }

        if (running)
        {
            Console.WriteLine();
            Console.WriteLine(
                "Press Enter to return to the menu."
            );

            Console.ReadLine();
        }
    }

    Console.WriteLine();
    Console.WriteLine("Invoice Generator closed.");
}
catch (Exception exception)
{
    Console.WriteLine();
    Console.WriteLine("The application failed to start.");
    Console.WriteLine(exception.Message);

    Console.WriteLine();
    Console.WriteLine("Press Enter to exit.");
    Console.ReadLine();
}

static string ReadMainMenuChoice()
{
    string[] options =
    [
        "Create invoice",
        "Create quote",
        "Search documents",
        "Manage quotes",
        "View counters",
        "Configure counters",
        "Configure service labor rate",
        "Exit"
    ];

    if (Console.IsInputRedirected)
    {
        ConsoleUi.Clear();

        Console.WriteLine("==================================");
        Console.WriteLine("       Invoice Generator");
        Console.WriteLine("==================================");

        for (int i = 0; i < options.Length; i++)
        {
            Console.WriteLine($"{i + 1}. {options[i]}");
        }

        Console.WriteLine();
        Console.Write("Select an option: ");

        return Console.ReadLine()?.Trim()
            ?? string.Empty;
    }

    int selectedIndex = 0;

    while (true)
    {
        Console.Clear();

        Console.WriteLine("==================================");
        Console.WriteLine("       Invoice Generator");
        Console.WriteLine("==================================");
        Console.WriteLine();
        Console.WriteLine(
            "Use Up/Down arrows and Enter. Number keys also work."
        );
        Console.WriteLine();

        for (int i = 0; i < options.Length; i++)
        {
            string marker =
                i == selectedIndex ? "> " : "  ";

            Console.WriteLine(
                $"{marker}{i + 1}. {options[i]}"
            );
        }

        ConsoleKeyInfo key =
            Console.ReadKey(intercept: true);

        switch (key.Key)
        {
            case ConsoleKey.UpArrow:
                selectedIndex =
                    selectedIndex == 0
                        ? options.Length - 1
                        : selectedIndex - 1;
                break;

            case ConsoleKey.DownArrow:
                selectedIndex =
                    selectedIndex == options.Length - 1
                        ? 0
                        : selectedIndex + 1;
                break;

            case ConsoleKey.Enter:
                Console.Clear();
                return (selectedIndex + 1).ToString();

            case ConsoleKey.D1:
            case ConsoleKey.NumPad1:
                Console.Clear();
                return "1";

            case ConsoleKey.D2:
            case ConsoleKey.NumPad2:
                Console.Clear();
                return "2";

            case ConsoleKey.D3:
            case ConsoleKey.NumPad3:
                Console.Clear();
                return "3";

            case ConsoleKey.D4:
            case ConsoleKey.NumPad4:
                Console.Clear();
                return "4";

            case ConsoleKey.D5:
            case ConsoleKey.NumPad5:
                Console.Clear();
                return "5";

            case ConsoleKey.D6:
            case ConsoleKey.NumPad6:
                Console.Clear();
                return "6";

            case ConsoleKey.D7:
            case ConsoleKey.NumPad7:
                Console.Clear();
                return "7";

            case ConsoleKey.D8:
            case ConsoleKey.NumPad8:
                Console.Clear();
                return "8";
        }
    }
}

static void CreateInvoice(AppDbContext db)
{
    InvoiceBuilder invoiceBuilder = new();
    string invoiceVariant =
        ReadInvoiceVariantChoice();

    DocumentRecord document =
        invoiceVariant switch
        {
            "Calibration" =>
                invoiceBuilder.BuildCalibrationInvoice(),
            "Service" =>
                invoiceBuilder.BuildServiceInvoice(),
            _ =>
                invoiceBuilder.BuildInvoice()
        };

    CreateDocument(
        db,
        document
    );
}

static string ReadInvoiceVariantChoice()
{
    string[] options =
    [
        "Regular invoice",
        "Calibration invoice",
        "Service invoice"
    ];

    if (Console.IsInputRedirected)
    {
        Console.WriteLine("1. Regular invoice");
        Console.WriteLine("2. Calibration invoice");
        Console.WriteLine("3. Service invoice");
        Console.Write("Select invoice type: ");

        string choice =
            Console.ReadLine()?.Trim()
            ?? string.Empty;

        return choice switch
        {
            "2" => "Calibration",
            "3" => "Service",
            _ => "Regular"
        };
    }

    int selectedIndex = 0;

    while (true)
    {
        Console.Clear();

        Console.WriteLine("==================================");
        Console.WriteLine("        Invoice Type");
        Console.WriteLine("==================================");
        Console.WriteLine();
        Console.WriteLine("Use Up/Down arrows and Enter.");
        Console.WriteLine();

        for (int i = 0; i < options.Length; i++)
        {
            string marker =
                i == selectedIndex ? "> " : "  ";

            Console.WriteLine($"{marker}{options[i]}");
        }

        ConsoleKeyInfo key =
            Console.ReadKey(intercept: true);

        switch (key.Key)
        {
            case ConsoleKey.UpArrow:
                selectedIndex =
                    selectedIndex == 0
                        ? options.Length - 1
                        : selectedIndex - 1;
                break;

            case ConsoleKey.DownArrow:
                selectedIndex =
                    selectedIndex == options.Length - 1
                        ? 0
                        : selectedIndex + 1;
                break;

            case ConsoleKey.D1:
            case ConsoleKey.NumPad1:
                Console.Clear();
                return "Regular";

            case ConsoleKey.D2:
            case ConsoleKey.NumPad2:
                Console.Clear();
                return "Calibration";

            case ConsoleKey.D3:
            case ConsoleKey.NumPad3:
                Console.Clear();
                return "Service";

            case ConsoleKey.Enter:
                Console.Clear();
                return selectedIndex switch
                {
                    1 => "Calibration",
                    2 => "Service",
                    _ => "Regular"
                };
        }
    }
}

static void CreateQuote(AppDbContext db)
{
    InvoiceBuilder invoiceBuilder = new();

    DocumentRecord document =
        invoiceBuilder.BuildQuote();

    CreateDocument(
        db,
        document
    );
}

static void CreateDocument(
    AppDbContext db,
    DocumentRecord document)
{
    InvoicePreviewService.Display(document);

    string documentType =
        document.DocumentType.ToLowerInvariant();

    Console.Write(
        $"Save and generate this {documentType}? (Y/N): "
    );

    string confirmation =
        Console.ReadLine()?.Trim().ToUpperInvariant()
        ?? string.Empty;

    if (confirmation != "Y")
    {
        Console.WriteLine();
        Console.WriteLine(
            $"{document.DocumentType} was not saved."
        );
        return;
    }

    GenerateDocument(
        db,
        document
    );
}

static void GenerateDocument(
    AppDbContext db,
    DocumentRecord document)
{
    string documentType =
        document.DocumentType.ToLowerInvariant();

    string? wordFilePath = null;
    string? pdfFilePath = null;
    string? documentNumber = null;
    AppSettings settings =
        AppSettingsService.Load();

    try
    {
        using IDbContextTransaction transaction =
            db.Database.BeginTransaction();

        try
        {
            // Save as draft first so the invoice gets a database ID.
            db.Documents.Add(document);
            db.SaveChanges();

            // Assign the next invoice number.
            DocumentNumberService numberService = new(db);

            documentNumber =
                numberService.AssignNextNumber(document);

            // Generate the Word invoice from the template.
            WordInvoiceService wordInvoiceService = new();

            wordFilePath =
                wordInvoiceService.Generate(document);

            // Update the saved database record.
            document.WordFilePath = wordFilePath;
            document.Status = DocumentStatus.Generated;

            PdfService pdfService = new();

            if (pdfService.IsAvailable())
            {
                pdfFilePath =
                    pdfService.ExportToPdf(wordFilePath);

                document.PdfFilePath = pdfFilePath;
                document.Status = DocumentStatus.Completed;

                if (settings.Pdf.DeleteWordDocumentAfterPdf)
                {
                    document.WordFilePath = null;

                    File.Delete(wordFilePath);
                    wordFilePath = null;
                }
            }

            db.SaveChanges();

            transaction.Commit();
        }
        catch
        {
            transaction.Rollback();

            DeleteFileIfExists(pdfFilePath);
            DeleteFileIfExists(wordFilePath);

            throw;
        }

        Console.WriteLine();
        Console.WriteLine(
            $"{document.DocumentType} generated successfully."
        );
        Console.WriteLine($"Database ID: {document.Id}");
        Console.WriteLine(
            $"{document.DocumentType} number: {documentNumber}"
        );
        Console.WriteLine($"Status: {document.Status}");

        if (pdfFilePath is not null)
        {
            Console.WriteLine($"PDF file: {pdfFilePath}");

            if (wordFilePath is null)
            {
                Console.WriteLine("Word file: removed after PDF generation.");
            }
            else
            {
                Console.WriteLine($"Word file: {wordFilePath}");
            }
        }
        else
        {
            Console.WriteLine($"Word file: {wordFilePath}");
            Console.WriteLine(
                "PDF file: skipped because Microsoft Word is not available on this computer."
            );
        }
    }
    catch (Exception exception)
    {
        Console.WriteLine();
        Console.WriteLine(
            $"The {documentType} could not be generated."
        );
        Console.WriteLine(exception.Message);

        if (exception.InnerException is not null)
        {
            Console.WriteLine();
            Console.WriteLine("Details:");
            Console.WriteLine(exception.InnerException.Message);
        }
    }
}

static void DeleteFileIfExists(string? filePath)
{
    if (string.IsNullOrWhiteSpace(filePath) ||
        !File.Exists(filePath))
    {
        return;
    }

    try
    {
        File.Delete(filePath);
    }
    catch
    {
        // Best effort cleanup. Preserve the original generation error.
    }
}

static void SearchDocuments(AppDbContext db)
{
    Console.WriteLine("==================================");
    Console.WriteLine("        Search Documents");
    Console.WriteLine("==================================");
    Console.WriteLine();

    Console.Write(
        "Search invoice number, customer, type, year/make, VIN, stock, PO, or claim: "
    );

    string searchText =
        Console.ReadLine()?.Trim()
        ?? string.Empty;

    if (string.IsNullOrWhiteSpace(searchText))
    {
        Console.WriteLine("Enter a search term.");
        return;
    }

    string pattern = $"%{searchText}%";

    List<DocumentRecord> documents =
        db.Documents
            .AsNoTracking()
            .Include(document => document.Items)
            .Where(document =>
                (document.DocumentNumber != null &&
                    EF.Functions.Like(
                        document.DocumentNumber,
                        pattern
                    )) ||
                EF.Functions.Like(
                    document.CustomerName,
                    pattern
                ) ||
                EF.Functions.Like(
                    document.InvoiceVariant,
                    pattern
                ) ||
                EF.Functions.Like(
                    document.YearAndMake,
                    pattern
                ) ||
                EF.Functions.Like(
                    document.Vin,
                    pattern
                ) ||
                EF.Functions.Like(
                    document.StockNumber,
                    pattern
                ) ||
                EF.Functions.Like(
                    document.PurchaseOrder,
                    pattern
                ) ||
                EF.Functions.Like(
                    document.ClaimNumber,
                    pattern
                ) ||
                EF.Functions.Like(
                    document.CalibrationLocation,
                    pattern
                ) ||
                EF.Functions.Like(
                    document.CalibrationType,
                    pattern
                ))
            .OrderByDescending(document => document.CreatedDate)
            .Take(25)
            .ToList();

    if (documents.Count == 0)
    {
        Console.WriteLine();
        Console.WriteLine("No matching documents were found.");
        return;
    }

    BrowseSearchResults(
        db,
        documents
    );
}

static void ManageQuotes(AppDbContext db)
{
    List<DocumentRecord> quotes =
        db.Documents
            .AsNoTracking()
            .Include(document => document.Items)
            .Where(document => document.DocumentType == "Quote")
            .OrderByDescending(document => document.CreatedDate)
            .ToList();

    if (quotes.Count == 0)
    {
        Console.WriteLine("No quotes were found.");
        return;
    }

    BrowseSearchResults(
        db,
        quotes,
        title: "Quotes"
    );
}

static void BrowseSearchResults(
    AppDbContext db,
    List<DocumentRecord> documents,
    string title = "Search Results")
{
    if (Console.IsInputRedirected)
    {
        RenderSearchResults(
            documents,
            selectedIndex: 0,
            title: title
        );

        Console.WriteLine();
        Console.Write("Enter a result number to view, or press Enter to return: ");

        string input = Console.ReadLine()?.Trim() ?? string.Empty;
        if (int.TryParse(input, out int selectedNumber) &&
            selectedNumber >= 1 &&
            selectedNumber <= documents.Count)
        {
            ShowDocumentDetails(db, documents[selectedNumber - 1]);
        }

        return;
    }

    int selectedIndex = 0;

    while (true)
    {
        RenderSearchResults(
            documents,
            selectedIndex,
            title
        );

        ConsoleKeyInfo key =
            Console.ReadKey(intercept: true);

        switch (key.Key)
        {
            case ConsoleKey.UpArrow:
                selectedIndex =
                    selectedIndex == 0
                        ? documents.Count - 1
                        : selectedIndex - 1;
                break;

            case ConsoleKey.DownArrow:
                selectedIndex =
                    selectedIndex == documents.Count - 1
                        ? 0
                        : selectedIndex + 1;
                break;

            case ConsoleKey.Enter:
                bool wasRemoved =
                    ShowDocumentDetails(
                        db,
                        documents[selectedIndex]
                    );

                if (wasRemoved)
                {
                    documents.RemoveAt(selectedIndex);

                    if (documents.Count == 0)
                    {
                        Console.Clear();
                        Console.WriteLine("No documents remain.");
                        return;
                    }

                    selectedIndex =
                        Math.Min(
                            selectedIndex,
                            documents.Count - 1
                        );
                }

                break;

            case ConsoleKey.Escape:
                return;
        }
    }
}

static void RenderSearchResults(
    List<DocumentRecord> documents,
    int selectedIndex,
    string title)
{
    ConsoleUi.Clear();

    Console.WriteLine("==================================");
    Console.WriteLine($"        {title}");
    Console.WriteLine("==================================");
    Console.WriteLine();
    Console.WriteLine(Console.IsInputRedirected
        ? "Enter the number of the document you want to view."
        : "Use Up/Down arrows, Enter to view, Esc to return.");
    Console.WriteLine();

    for (int i = 0; i < documents.Count; i++)
    {
        DocumentRecord document = documents[i];

        string marker = Console.IsInputRedirected
            ? $"{i + 1}. "
            : i == selectedIndex ? "> " : "  ";

        Console.WriteLine(
            $"{marker}{document.DocumentNumber ?? "(draft)",-8} " +
            $"{GetDocumentDisplayType(document),-11} " +
            $"{document.CreatedDate:yyyy-MM-dd}  " +
            $"{document.CustomerName,-24} " +
            $"{document.Total,10:C2}  " +
            $"{document.Status}"
        );
    }

    if (documents.Count == 25)
    {
        Console.WriteLine();
        Console.WriteLine(
            "Showing first 25 matches. Use a more specific search to narrow results."
        );
    }
}

static string GetDocumentDisplayType(DocumentRecord document)
{
    if (IsCalibration(document))
    {
        return "Calibration";
    }

    if (IsService(document))
    {
        return "Service";
    }

    return document.DocumentType;
}

static bool IsCalibration(DocumentRecord document)
{
    return string.Equals(
        document.InvoiceVariant,
        "Calibration",
        StringComparison.OrdinalIgnoreCase
    );
}

static bool IsService(DocumentRecord document)
{
    return string.Equals(
        document.InvoiceVariant,
        "Service",
        StringComparison.OrdinalIgnoreCase
    );
}

static bool ShowDocumentDetails(
    AppDbContext db,
    DocumentRecord document)
{
    while (true)
    {
        ConsoleUi.Clear();

        Console.WriteLine("==================================");
        Console.WriteLine("        Document Details");
        Console.WriteLine("==================================");
        Console.WriteLine();

        Console.WriteLine(
            $"Number:   {document.DocumentNumber ?? "(draft)"}"
        );
        Console.WriteLine($"Type:     {document.DocumentType}");
        if (IsCalibration(document))
        {
            Console.WriteLine("Variant:  Calibration");
        }
        else if (IsService(document))
        {
            Console.WriteLine("Variant:  Service");
        }
        Console.WriteLine(
            $"Date:     {document.CreatedDate:yyyy-MM-dd}"
        );
        Console.WriteLine($"Status:   {document.Status}");
        Console.WriteLine($"Customer: {document.CustomerName}");
        Console.WriteLine($"Vehicle:  {document.YearAndMake}");
        Console.WriteLine($"VIN:      {document.Vin}");
        if (IsCalibration(document))
        {
            Console.WriteLine($"Claim:    {document.ClaimNumber}");
            Console.WriteLine($"Location: {document.CalibrationLocation}");
            Console.WriteLine($"Type:     {document.CalibrationType}");
        }
        else
        {
            Console.WriteLine($"Stock:    {document.StockNumber}");
        }
        Console.WriteLine($"PO:       {document.PurchaseOrder}");
        Console.WriteLine();

        if (!IsCalibration(document))
        {
            Console.WriteLine(
                "QTY      ITEM       DESCRIPTION"
            );

            Console.WriteLine(
                "----------------------------------------------"
            );

            foreach (InvoiceItem item in document.Items)
            {
                Console.WriteLine(
                    $"{item.Quantity,-8}" +
                    $"{item.ItemNumber,-11}" +
                    $"{item.Description}"
                );

                Console.WriteLine(
                    $"         {item.UnitPrice,10:C2}" +
                    $"  Discount: {item.Discount,10:C2}" +
                    $"  Line: {item.LineTotal,10:C2}"
                );
            }
        }

        Console.WriteLine();
        Console.WriteLine(
            $"Subtotal: {document.Subtotal,15:C2}"
        );
        Console.WriteLine(
            $"GST 5%:   {document.Gst,15:C2}"
        );
        Console.WriteLine(
            $"PST 7%:   {document.Pst,15:C2}"
        );
        Console.WriteLine(
            $"Total:    {document.Total,15:C2}"
        );
        Console.WriteLine();

        if (!string.IsNullOrWhiteSpace(document.PdfFilePath))
        {
            Console.WriteLine($"PDF:  {document.PdfFilePath}");
        }

        if (!string.IsNullOrWhiteSpace(document.WordFilePath))
        {
            Console.WriteLine($"Word: {document.WordFilePath}");
        }

        Console.WriteLine();
        if (string.Equals(
                document.DocumentType,
                "Quote",
                StringComparison.OrdinalIgnoreCase))
        {
            Console.WriteLine(
                "Press C to convert this quote to an invoice."
            );
            Console.WriteLine(
                "Press D to invalidate/remove this quote from the database."
            );
        }

        Console.WriteLine(
            "Press Esc or Enter to return to the list."
        );

        if (Console.IsInputRedirected)
        {
            bool isQuote = string.Equals(
                document.DocumentType,
                "Quote",
                StringComparison.OrdinalIgnoreCase);

            Console.Write(isQuote
                ? "Enter C to convert, D to remove, or press Enter to return: "
                : "Press Enter to return: ");

            string redirectedChoice =
                Console.ReadLine()?.Trim().ToUpperInvariant()
                ?? string.Empty;

            if (isQuote && redirectedChoice == "C")
            {
                ConvertQuoteToInvoice(db, document);
            }
            else if (isQuote && redirectedChoice == "D")
            {
                return RemoveQuote(db, document);
            }

            return false;
        }

        ConsoleKeyInfo key =
            Console.ReadKey(intercept: true);

        if (key.Key == ConsoleKey.C &&
            string.Equals(
                document.DocumentType,
                "Quote",
                StringComparison.OrdinalIgnoreCase))
        {
            ConvertQuoteToInvoice(
                db,
                document
            );

            return false;
        }

        if (key.Key == ConsoleKey.D &&
            string.Equals(
                document.DocumentType,
                "Quote",
                StringComparison.OrdinalIgnoreCase))
        {
            return RemoveQuote(
                db,
                document
            );
        }

        if (key.Key is ConsoleKey.Escape or ConsoleKey.Enter)
        {
            return false;
        }
    }
}

static bool RemoveQuote(
    AppDbContext db,
    DocumentRecord quote)
{
    Console.WriteLine();
    Console.Write(
        $"Remove quote {quote.DocumentNumber} from the database? (Y/N): "
    );

    string confirmation =
        Console.ReadLine()?.Trim().ToUpperInvariant()
        ?? string.Empty;

    if (confirmation != "Y")
    {
        Console.WriteLine("Quote was not removed.");
        return false;
    }

    DocumentRecord? quoteToRemove =
        db.Documents
            .Include(document => document.Items)
            .SingleOrDefault(document =>
                document.Id == quote.Id &&
                document.DocumentType == "Quote");

    if (quoteToRemove is null)
    {
        Console.WriteLine("Quote was not found.");
        return false;
    }

    string? pdfFilePath = quoteToRemove.PdfFilePath;
    string? wordFilePath = quoteToRemove.WordFilePath;

    db.Documents.Remove(quoteToRemove);
    db.SaveChanges();

    DeleteFileIfExists(pdfFilePath);
    DeleteFileIfExists(wordFilePath);

    Console.WriteLine("Quote was removed.");
    return true;
}

static void ConvertQuoteToInvoice(
    AppDbContext db,
    DocumentRecord quote)
{
    Console.WriteLine();
    Console.Write(
        $"Convert quote {quote.DocumentNumber} to an invoice? (Y/N): "
    );

    string confirmation =
        Console.ReadLine()?.Trim().ToUpperInvariant()
        ?? string.Empty;

    if (confirmation != "Y")
    {
        Console.WriteLine("Quote was not converted.");
        return;
    }

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
        Items = quote.Items
            .Select(item => new InvoiceItem
            {
                Quantity = item.Quantity,
                ItemNumber = item.ItemNumber,
                Description = item.Description,
                UnitPrice = item.UnitPrice,
                Discount = item.Discount,
                LineTotal = item.LineTotal
            })
            .ToList()
    };

    GenerateDocument(
        db,
        invoice
    );
}

static void ShowCounters(AppDbContext db)
{
    const int documentNumberLength = 6;

    List<DocumentCounter> counters =
        db.DocumentCounters
            .OrderBy(counter => counter.Id)
            .ToList();

    if (counters.Count == 0)
    {
        Console.WriteLine("No counters were found.");
        return;
    }

    foreach (DocumentCounter counter in counters)
    {
        string currentNumber =
            counter.CurrentNumber.ToString(
                $"D{documentNumberLength}"
            );

        Console.WriteLine(
            $"{counter.DocumentType}: {currentNumber}"
        );
    }
}

static void ConfigureCounters(AppDbContext db)
{
    CounterSettingsService settingsService = new(db);

    Console.WriteLine("==================================");
    Console.WriteLine("        Configure Counters");
    Console.WriteLine("==================================");
    Console.WriteLine();

    Console.WriteLine("1. Set invoice counter");
    Console.WriteLine("2. Set quote counter");
    Console.WriteLine("3. Cancel");
    Console.WriteLine();

    Console.Write("Select an option: ");

    string choice =
        Console.ReadLine()?.Trim()
        ?? string.Empty;

    string documentType;

    switch (choice)
    {
        case "1":
            documentType = "Invoice";
            break;

        case "2":
            documentType = "Quote";
            break;

        default:
            Console.WriteLine("No changes were made.");
            return;
    }

    DocumentCounter? counter =
        db.DocumentCounters.SingleOrDefault(
            item => item.DocumentType == documentType
        );

    if (counter is null)
    {
        Console.WriteLine(
            $"{documentType} counter was not found."
        );

        return;
    }

    Console.WriteLine();
    Console.WriteLine(
        $"Current stored number: {counter.CurrentNumber}"
    );

    Console.Write(
        "Enter the last 6-digit number already used: "
    );

    string input =
        Console.ReadLine()?.Trim()
        ?? string.Empty;

    if (!int.TryParse(input, out int newNumber) ||
        newNumber < 0)
    {
        Console.WriteLine(
            "Enter a valid non-negative whole number."
        );

        return;
    }

    settingsService.SetCurrentNumber(
        documentType,
        newNumber
    );

    Console.WriteLine();
    Console.WriteLine(
        $"{documentType} counter updated to {newNumber}."
    );

    Console.WriteLine(
        $"The next {documentType.ToLowerInvariant()} " +
        $"will use number {newNumber + 1}."
    );
}

static void ConfigureServiceLaborRate()
{
    AppSettings settings =
        AppSettingsService.Load();

    Console.WriteLine("==================================");
    Console.WriteLine("   Configure Service Labor Rate");
    Console.WriteLine("==================================");
    Console.WriteLine();

    Console.WriteLine(
        $"Current labor rate: {settings.Service.LaborRate:C2}"
    );
    Console.Write(
        "Enter the new labor rate, or leave blank to cancel: "
    );

    string input =
        Console.ReadLine()?.Trim()
        ?? string.Empty;

    if (string.IsNullOrWhiteSpace(input))
    {
        Console.WriteLine("No changes were made.");
        return;
    }

    string normalizedInput =
        input
            .Replace("$", string.Empty)
            .Replace(",", string.Empty);

    if (!decimal.TryParse(
            normalizedInput,
            System.Globalization.NumberStyles.Number,
            System.Globalization.CultureInfo.InvariantCulture,
            out decimal laborRate) ||
        laborRate <= 0)
    {
        Console.WriteLine(
            "Enter a valid labor rate greater than zero."
        );
        return;
    }

    settings.Service.LaborRate = Math.Round(
        laborRate,
        2,
        MidpointRounding.AwayFromZero
    );

    AppSettingsService.Save(settings);

    Console.WriteLine(
        $"Service labor rate was updated to {settings.Service.LaborRate:C2}."
    );
}
