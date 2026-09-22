using InvoiceGenerator.Models;
using System.Globalization;

namespace InvoiceGenerator.Services;

public class InvoiceBuilder
{
    public static void Recalculate(DocumentRecord document)
    {
        if (IsCalibration(document)) ApplyCalibrationTotal(document);
        else CalculateTotals(document);
    }
    private const decimal GstRate = 0.05m;
    private const decimal PstRate = 0.07m;
    private const decimal CalibrationSubtotal = 225m;
    private const decimal UniversalCalibrationSubtotal = 450m;

    private static readonly string[] ServiceDescriptions =
    [
        "MAINTENANCE SERVICE",
        "MOTOR OIL",
        "OIL DRAIN PLUG",
        "OIL FILTER",
        "SHOP SUPPLY"
    ];

    private static readonly string[] CalibrationLocations =
    [
        "front-end",
        "rear-end",
        "side mirror"
    ];

    private static readonly string[] CalibrationTypes =
    [
        "static",
        "dynamic",
        "universal"
    ];

    public DocumentRecord BuildInvoice()
    {
        return BuildDocument("Invoice");
    }

    public DocumentRecord BuildCalibrationInvoice()
    {
        DocumentRecord document = new()
        {
            DocumentType = "Invoice",
            InvoiceVariant = "Calibration",
            CreatedDate = DateTime.Now,
            Status = DocumentStatus.Draft
        };

        int step = 0;
        const int totalSteps = 7;

        while (step < totalSteps)
        {
            RenderCreateInvoiceScreen(
                document,
                itemCount: null,
                step,
                "Calibration Invoice"
            );

            bool goBack =
                step switch
                {
                    0 => ReadCustomerName(document),
                    1 => ReadYearAndMake(document),
                    2 => ReadVin(document),
                    3 => ReadClaimNumber(document),
                    4 => ReadCalibrationLocation(document),
                    5 => ReadCalibrationType(document),
                    6 => ReadPurchaseOrder(document),
                    _ => false
                };

            if (goBack)
            {
                if (step > 0)
                {
                    step--;
                }

                continue;
            }

            step++;
        }

        ApplyCalibrationTotal(document);

        return document;
    }

    public DocumentRecord BuildServiceInvoice()
    {
        DocumentRecord document = new()
        {
            DocumentType = "Invoice",
            InvoiceVariant = "Service",
            CreatedDate = DateTime.Now,
            Status = DocumentStatus.Draft
        };

        foreach (string description in ServiceDescriptions)
        {
            document.Items.Add(new InvoiceItem
            {
                Description = description
            });
        }

        decimal serviceLaborRate =
            AppSettingsService.Load().Service.LaborRate;

        document.Items[0].ItemNumber = "LABOUR";
        document.Items[0].UnitPrice = serviceLaborRate;

        int step = 0;
        int totalSteps = 5 + 1 + (ServiceDescriptions.Length - 1) * 2;

        while (step < totalSteps)
        {
            RenderCreateInvoiceScreen(
                document,
                itemCount: ServiceDescriptions.Length,
                step,
                "Service Invoice"
            );

            bool goBack =
                step switch
                {
                    0 => ReadCustomerName(document),
                    1 => ReadYearAndMake(document),
                    2 => ReadVin(document),
                    3 => ReadStockNumber(document),
                    4 => ReadPurchaseOrder(document),
                    5 => ReadServiceLaborHours(document),
                    _ => ReadServiceItemField(
                        document,
                        step
                    )
                };

            if (goBack)
            {
                if (step > 0)
                {
                    step--;
                }

                continue;
            }

            step++;
        }

        CalculateTotals(document);

        return document;
    }

    public DocumentRecord BuildQuote()
    {
        return BuildDocument("Quote");
    }

    private DocumentRecord BuildDocument(string documentType)
    {
        DocumentRecord document = new()
        {
            DocumentType = documentType,
            CreatedDate = DateTime.Now,
            Status = DocumentStatus.Draft
        };

        int? itemCount = null;
        int step = 0;

        while (true)
        {
            int totalSteps =
                itemCount.HasValue
                    ? 6 + itemCount.Value * 5
                    : 6;

            if (step >= totalSteps)
            {
                break;
            }

            RenderCreateInvoiceScreen(
                document,
                itemCount,
                step,
                documentType
            );

            bool goBack =
                step switch
                {
                    0 => ReadCustomerName(document),
                    1 => ReadYearAndMake(document),
                    2 => ReadVin(document),
                    3 => ReadStockNumber(document),
                    4 => ReadPurchaseOrder(document),
                    5 => ReadItemCount(
                        document,
                        ref itemCount
                    ),
                    _ => ReadItemField(
                        document,
                        step,
                        itemCount!.Value
                    )
                };

            if (goBack)
            {
                if (step > 0)
                {
                    step--;
                }

                continue;
            }

            step++;
        }

        CalculateTotals(document);

        return document;
    }

    private static bool ReadCustomerName(DocumentRecord document)
    {
        PromptResult<string> result =
            ReadRequiredText(
                "Customer name",
                document.CustomerName
            );

        if (result.Back)
        {
            return true;
        }

        document.CustomerName = result.Value;
        return false;
    }

    private static bool ReadYearAndMake(DocumentRecord document)
    {
        PromptResult<string> result =
            ReadOptionalText(
                "Year and make",
                document.YearAndMake
            );

        if (result.Back)
        {
            return true;
        }

        document.YearAndMake = result.Value;
        return false;
    }

    private static bool ReadVin(DocumentRecord document)
    {
        PromptResult<string> result =
            ReadOptionalText(
                "VIN",
                document.Vin
            );

        if (result.Back)
        {
            return true;
        }

        document.Vin = result.Value;
        return false;
    }

    private static bool ReadStockNumber(DocumentRecord document)
    {
        PromptResult<string> result =
            ReadOptionalText(
                "Stock number",
                document.StockNumber
            );

        if (result.Back)
        {
            return true;
        }

        document.StockNumber = result.Value;
        return false;
    }

    private static bool ReadPurchaseOrder(DocumentRecord document)
    {
        PromptResult<string> result =
            ReadOptionalText(
                "PO number",
                document.PurchaseOrder
            );

        if (result.Back)
        {
            return true;
        }

        document.PurchaseOrder = result.Value;
        return false;
    }

    private static bool ReadClaimNumber(DocumentRecord document)
    {
        PromptResult<string> result =
            ReadOptionalText(
                "Claim number",
                document.ClaimNumber
            );

        if (result.Back)
        {
            return true;
        }

        document.ClaimNumber = result.Value;
        return false;
    }

    private static bool ReadCalibrationLocation(DocumentRecord document)
    {
        PromptResult<string> result =
            ReadChoice(
                "Calibration location",
                CalibrationLocations,
                document.CalibrationLocation
            );

        if (result.Back)
        {
            return true;
        }

        document.CalibrationLocation = result.Value;
        return false;
    }

    private static bool ReadCalibrationType(DocumentRecord document)
    {
        PromptResult<string> result =
            ReadChoice(
                "Calibration type",
                CalibrationTypes,
                document.CalibrationType
            );

        if (result.Back)
        {
            return true;
        }

        document.CalibrationType = result.Value;
        return false;
    }

    private static bool ReadItemCount(
        DocumentRecord document,
        ref int? itemCount)
    {
        PromptResult<int> result =
            ReadPositiveInteger(
                "How many line items",
                itemCount
            );

        if (result.Back)
        {
            return true;
        }

        itemCount = result.Value;

        while (document.Items.Count < itemCount.Value)
        {
            document.Items.Add(new InvoiceItem());
        }

        while (document.Items.Count > itemCount.Value)
        {
            document.Items.RemoveAt(
                document.Items.Count - 1
            );
        }

        return false;
    }

    private static bool ReadItemField(
        DocumentRecord document,
        int step,
        int itemCount)
    {
        int itemIndex = (step - 6) / 5;
        int fieldIndex = (step - 6) % 5;

        if (itemIndex >= itemCount)
        {
            return false;
        }

        InvoiceItem item = document.Items[itemIndex];

        bool goBack =
            fieldIndex switch
            {
                0 => ReadItemQuantity(item),
                1 => ReadItemNumber(item),
                2 => ReadItemDescription(item),
                3 => ReadItemUnitPrice(item),
                4 => ReadItemDiscount(item),
                _ => false
            };

        CalculateItemTotal(item);

        return goBack;
    }

    private static bool ReadServiceLaborHours(DocumentRecord document)
    {
        InvoiceItem laborItem = document.Items[0];

        PromptResult<decimal> result =
            ReadPositiveDecimal(
                "Labor hours",
                laborItem.Quantity > 0
                    ? laborItem.Quantity
                    : null
            );

        if (result.Back)
        {
            return true;
        }

        laborItem.Quantity = result.Value;
        CalculateItemTotal(laborItem);
        return false;
    }

    private static bool ReadServiceItemField(
        DocumentRecord document,
        int step)
    {
        int serviceStep = step - 6;
        int itemIndex = 1 + serviceStep / 2;
        int fieldIndex = serviceStep % 2;

        if (itemIndex >= document.Items.Count)
        {
            return false;
        }

        InvoiceItem item = document.Items[itemIndex];

        bool goBack =
            fieldIndex == 0
                ? ReadServiceItemQuantity(item)
                : ReadServiceItemUnitPrice(item);

        CalculateItemTotal(item);
        return goBack;
    }

    private static bool ReadServiceItemQuantity(InvoiceItem item)
    {
        PromptResult<decimal> result =
            ReadNonNegativeDecimal(
                $"{item.Description} quantity",
                item.Quantity
            );

        if (result.Back)
        {
            return true;
        }

        item.Quantity = result.Value;
        return false;
    }

    private static bool ReadServiceItemUnitPrice(InvoiceItem item)
    {
        PromptResult<decimal> result =
            ReadNonNegativeDecimal(
                $"{item.Description} unit price",
                item.UnitPrice
            );

        if (result.Back)
        {
            return true;
        }

        item.UnitPrice = result.Value;
        return false;
    }

    private static bool ReadItemQuantity(InvoiceItem item)
    {
        PromptResult<decimal> result =
            ReadPositiveDecimal(
                "Quantity",
                item.Quantity > 0 ? item.Quantity : null
            );

        if (result.Back)
        {
            return true;
        }

        item.Quantity = result.Value;
        return false;
    }

    private static bool ReadItemNumber(InvoiceItem item)
    {
        PromptResult<string> result =
            ReadOptionalText(
                "Item number",
                item.ItemNumber
            );

        if (result.Back)
        {
            return true;
        }

        item.ItemNumber = result.Value;
        return false;
    }

    private static bool ReadItemDescription(InvoiceItem item)
    {
        PromptResult<string> result =
            ReadRequiredText(
                "Description",
                item.Description
            );

        if (result.Back)
        {
            return true;
        }

        item.Description = result.Value;
        return false;
    }

    private static bool ReadItemUnitPrice(InvoiceItem item)
    {
        PromptResult<decimal> result =
            ReadNonNegativeDecimal(
                "Unit price",
                item.UnitPrice
            );

        if (result.Back)
        {
            return true;
        }

        item.UnitPrice = result.Value;
        return false;
    }

    private static bool ReadItemDiscount(InvoiceItem item)
    {
        while (true)
        {
            PromptResult<decimal> result =
                ReadNonNegativeDecimal(
                    "Discount amount",
                    item.Discount
                );

            if (result.Back)
            {
                return true;
            }

            decimal grossAmount =
                item.Quantity * item.UnitPrice;

            if (result.Value <= grossAmount)
            {
                item.Discount = result.Value;
                return false;
            }

            Console.WriteLine(
                "Discount cannot be greater than the item amount."
            );
        }
    }

    private static void RenderCreateInvoiceScreen(
        DocumentRecord document,
        int? itemCount,
        int step,
        string documentType)
    {
        ConsoleUi.Clear();

        Console.WriteLine("==================================");
        Console.WriteLine(
            $"         Create {documentType}"
        );
        Console.WriteLine("==================================");
        Console.WriteLine("Press Esc to go back one field.");
        Console.WriteLine();

        Console.WriteLine($"Customer:      {document.CustomerName}");
        Console.WriteLine($"Year/make:     {document.YearAndMake}");
        Console.WriteLine($"VIN:           {document.Vin}");
        if (IsCalibration(document))
        {
            Console.WriteLine($"Claim number:  {document.ClaimNumber}");
            Console.WriteLine($"Location:      {document.CalibrationLocation}");
            Console.WriteLine($"Type:          {document.CalibrationType}");
        }
        else
        {
            Console.WriteLine($"Stock number:  {document.StockNumber}");
        }
        Console.WriteLine($"PO number:     {document.PurchaseOrder}");
        if (IsService(document))
        {
            Console.WriteLine("Service rows:");

            foreach (InvoiceItem item in document.Items)
            {
                Console.WriteLine(
                    $"  {item.Description,-20} " +
                    $"Qty: {FormatDecimal(item.Quantity),-6} " +
                    $"Unit: {FormatCurrency(item.UnitPrice),-10} " +
                    $"Line: {FormatCurrency(item.LineTotal)}"
                );
            }
        }
        else if (!IsCalibration(document))
        {
            Console.WriteLine(
                $"Line items:    {(itemCount.HasValue ? itemCount : "")}"
            );
        }

        if (!IsService(document) &&
            step >= 6 &&
            itemCount.HasValue)
        {
            int itemIndex = (step - 6) / 5;

            Console.WriteLine();
            Console.WriteLine(
                $"Item {itemIndex + 1} of {itemCount.Value}"
            );
            Console.WriteLine("----------------------------------");

            if (itemIndex < document.Items.Count)
            {
                InvoiceItem item = document.Items[itemIndex];

                Console.WriteLine(
                    $"Quantity:      {FormatDecimal(item.Quantity)}"
                );
                Console.WriteLine(
                    $"Item number:   {item.ItemNumber}"
                );
                Console.WriteLine(
                    $"Description:   {item.Description}"
                );
                Console.WriteLine(
                    $"Unit price:    {FormatCurrency(item.UnitPrice)}"
                );
                Console.WriteLine(
                    $"Discount:      {FormatCurrency(item.Discount)}"
                );
            }
        }

        Console.WriteLine();
    }

    private static PromptResult<string> ReadRequiredText(
        string label,
        string currentValue)
    {
        while (true)
        {
            PromptResult<string> result =
                ReadText(
                    label,
                    currentValue,
                    keepCurrentWhenBlank: true
                );

            if (result.Back)
            {
                return result;
            }

            if (!string.IsNullOrWhiteSpace(result.Value))
            {
                return result;
            }

            Console.WriteLine("This field is required.");
        }
    }

    private static PromptResult<string> ReadOptionalText(
        string label,
        string currentValue)
    {
        return ReadText(
            label,
            currentValue,
            keepCurrentWhenBlank: false
        );
    }

    private static PromptResult<int> ReadPositiveInteger(
        string label,
        int? currentValue)
    {
        while (true)
        {
            PromptResult<string> result =
                ReadText(
                    label,
                    currentValue?.ToString() ?? string.Empty,
                    keepCurrentWhenBlank: true
                );

            if (result.Back)
            {
                return PromptResult<int>.GoBack();
            }

            if (int.TryParse(result.Value, out int value) &&
                value > 0)
            {
                return PromptResult<int>.FromValue(value);
            }

            Console.WriteLine(
                "Enter a whole number greater than zero."
            );
        }
    }

    private static PromptResult<decimal> ReadPositiveDecimal(
        string label,
        decimal? currentValue)
    {
        while (true)
        {
            PromptResult<decimal> result =
                ReadDecimal(label, currentValue);

            if (result.Back)
            {
                return result;
            }

            if (result.Value > 0)
            {
                return result;
            }

            Console.WriteLine(
                "Enter a number greater than zero."
            );
        }
    }

    private static PromptResult<decimal> ReadNonNegativeDecimal(
        string label,
        decimal? currentValue)
    {
        while (true)
        {
            PromptResult<decimal> result =
                ReadDecimal(label, currentValue);

            if (result.Back)
            {
                return result;
            }

            if (result.Value >= 0)
            {
                return result;
            }

            Console.WriteLine(
                "Enter zero or a positive number."
            );
        }
    }

    private static PromptResult<decimal> ReadDecimal(
        string label,
        decimal? currentValue)
    {
        while (true)
        {
            PromptResult<string> result =
                ReadText(
                    label,
                    currentValue.HasValue
                        ? FormatDecimal(currentValue.Value)
                        : string.Empty,
                    keepCurrentWhenBlank: true
                );

            if (result.Back)
            {
                return PromptResult<decimal>.GoBack();
            }

            string input = result.Value
                .Replace("$", string.Empty)
                .Replace(",", string.Empty);

            if (decimal.TryParse(
                    input,
                    NumberStyles.Number,
                    CultureInfo.InvariantCulture,
                    out decimal value))
            {
                return PromptResult<decimal>.FromValue(value);
            }

            Console.WriteLine(
                "Enter a valid number, such as 125.50."
            );
        }
    }

    private static PromptResult<string> ReadText(
        string label,
        string currentValue,
        bool keepCurrentWhenBlank)
    {
        string prompt =
            string.IsNullOrWhiteSpace(currentValue)
                ? $"{label}: "
                : $"{label} [{currentValue}]: ";

        Console.Write(prompt);

        if (Console.IsInputRedirected)
        {
            string redirectedValue =
                Console.ReadLine()?.Trim()
                ?? string.Empty;

            if (keepCurrentWhenBlank &&
                string.IsNullOrEmpty(redirectedValue) &&
                !string.IsNullOrWhiteSpace(currentValue))
            {
                redirectedValue = currentValue;
            }

            return PromptResult<string>.FromValue(
                DecodeEscapedNewLines(redirectedValue)
            );
        }

        List<char> characters = new();

        while (true)
        {
            ConsoleKeyInfo key =
                Console.ReadKey(intercept: true);

            if (key.Key == ConsoleKey.Escape)
            {
                Console.WriteLine();
                return PromptResult<string>.GoBack();
            }

            if (key.Key == ConsoleKey.Enter)
            {
                Console.WriteLine();

                string value =
                    new string(characters.ToArray()).Trim();

                if (keepCurrentWhenBlank &&
                    string.IsNullOrEmpty(value) &&
                    !string.IsNullOrWhiteSpace(currentValue))
                {
                    value = currentValue;
                }

                return PromptResult<string>.FromValue(
                    DecodeEscapedNewLines(value)
                );
            }

            if (key.Key == ConsoleKey.Backspace)
            {
                if (characters.Count == 0)
                {
                    continue;
                }

                characters.RemoveAt(characters.Count - 1);
                Console.Write("\b \b");
                continue;
            }

            if (char.IsControl(key.KeyChar))
            {
                continue;
            }

            characters.Add(key.KeyChar);
            Console.Write(key.KeyChar);
        }
    }

    private static string DecodeEscapedNewLines(string value)
    {
        return value
            .Replace("\\r\\n", "\n")
            .Replace("\\n", "\n")
            .Replace("\\r", "\n");
    }

    private static PromptResult<string> ReadChoice(
        string label,
        string[] options,
        string currentValue)
    {
        int selectedIndex =
            Math.Max(
                Array.FindIndex(
                    options,
                    option => string.Equals(
                        option,
                        currentValue,
                        StringComparison.OrdinalIgnoreCase
                    )
                ),
                0
            );

        if (Console.IsInputRedirected)
        {
            Console.WriteLine(label);

            for (int i = 0; i < options.Length; i++)
            {
                Console.WriteLine($"{i + 1}. {options[i]}");
            }

            Console.Write("Select an option: ");

            string input =
                Console.ReadLine()?.Trim()
                ?? string.Empty;

            if (int.TryParse(input, out int choice) &&
                choice >= 1 &&
                choice <= options.Length)
            {
                return PromptResult<string>.FromValue(
                    options[choice - 1]
                );
            }

            return PromptResult<string>.FromValue(
                options[selectedIndex]
            );
        }

        while (true)
        {
            Console.Clear();

            Console.WriteLine("==================================");
            Console.WriteLine($"        {label}");
            Console.WriteLine("==================================");
            Console.WriteLine();
            Console.WriteLine(
                "Use Up/Down arrows and Enter. Press Esc to go back."
            );
            Console.WriteLine();

            for (int i = 0; i < options.Length; i++)
            {
                string marker =
                    i == selectedIndex ? "> " : "  ";

                Console.WriteLine($"{marker}{i + 1}. {options[i]}");
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

                case ConsoleKey.Escape:
                    return PromptResult<string>.GoBack();

                case ConsoleKey.Enter:
                    return PromptResult<string>.FromValue(
                        options[selectedIndex]
                    );

                case ConsoleKey.D1:
                case ConsoleKey.NumPad1:
                    return PromptResult<string>.FromValue(options[0]);

                case ConsoleKey.D2:
                case ConsoleKey.NumPad2:
                    if (options.Length >= 2)
                    {
                        return PromptResult<string>.FromValue(options[1]);
                    }
                    break;

                case ConsoleKey.D3:
                case ConsoleKey.NumPad3:
                    if (options.Length >= 3)
                    {
                        return PromptResult<string>.FromValue(options[2]);
                    }
                    break;
            }
        }
    }

    private static void CalculateItemTotal(InvoiceItem item)
    {
        decimal lineTotal =
            item.Quantity * item.UnitPrice - item.Discount;

        item.LineTotal = Math.Round(
            Math.Max(lineTotal, 0),
            2,
            MidpointRounding.AwayFromZero
        );
    }

    private static string FormatCurrency(decimal value)
    {
        return value == 0 ? string.Empty : value.ToString("C2");
    }

    private static string FormatDecimal(decimal value)
    {
        return value == 0 ? string.Empty : value.ToString("0.##");
    }

    private static bool IsCalibration(DocumentRecord document)
    {
        return string.Equals(
            document.InvoiceVariant,
            "Calibration",
            StringComparison.OrdinalIgnoreCase
        );
    }

    private static bool IsService(DocumentRecord document)
    {
        return string.Equals(
            document.InvoiceVariant,
            "Service",
            StringComparison.OrdinalIgnoreCase
        );
    }

    private readonly struct PromptResult<T>
    {
        private PromptResult(T value, bool back)
        {
            Value = value;
            Back = back;
        }

        public T Value { get; }

        public bool Back { get; }

        public static PromptResult<T> FromValue(T value)
        {
            return new PromptResult<T>(value, back: false);
        }

        public static PromptResult<T> GoBack()
        {
            return new PromptResult<T>(default!, back: true);
        }
    }

    private static void CalculateTotals(DocumentRecord document)
    {
        foreach (InvoiceItem item in document.Items)
        {
            CalculateItemTotal(item);
        }

        document.Subtotal = Math.Round(
            document.Items.Sum(item => item.LineTotal),
            2,
            MidpointRounding.AwayFromZero
        );

        document.Gst = Math.Round(
            document.Subtotal * GstRate,
            2,
            MidpointRounding.AwayFromZero
        );

        document.Pst = Math.Round(
            document.Subtotal * PstRate,
            2,
            MidpointRounding.AwayFromZero
        );

        document.Total = Math.Round(
            document.Subtotal + document.Gst + document.Pst,
            2,
            MidpointRounding.AwayFromZero
        );
    }

    private static void ApplyCalibrationTotal(DocumentRecord document)
    {
        document.Subtotal =
            string.Equals(
                document.CalibrationType,
                "universal",
                StringComparison.OrdinalIgnoreCase
            )
                ? UniversalCalibrationSubtotal
                : CalibrationSubtotal;

        document.Gst = Math.Round(
            document.Subtotal * GstRate,
            2,
            MidpointRounding.AwayFromZero
        );
        document.Pst = Math.Round(
            document.Subtotal * PstRate,
            2,
            MidpointRounding.AwayFromZero
        );
        document.Total = Math.Round(
            document.Subtotal + document.Gst + document.Pst,
            2,
            MidpointRounding.AwayFromZero
        );
    }
}
