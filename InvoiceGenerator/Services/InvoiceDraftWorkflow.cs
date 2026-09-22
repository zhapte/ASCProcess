using System.Globalization;
using InvoiceGenerator.Models;

namespace InvoiceGenerator.Services;

public sealed record InvoicePrompt(string Key, string Label, string Default = "", bool Required = false,
    bool Numeric = false, bool Positive = false, string[]? Choices = null);

public sealed class InvoiceDraftWorkflow
{
    public string Variant { get; set; } = "Regular";
    public Dictionary<string, string> Values { get; } = new();
    private static readonly string[] ServiceItems = ["MAINTENANCE SERVICE", "MOTOR OIL", "OIL DRAIN PLUG", "OIL FILTER", "SHOP SUPPLY"];
    public string Value(InvoicePrompt prompt) => Values.GetValueOrDefault(prompt.Key, prompt.Default);
    public List<InvoicePrompt> Prompts()
    {
        List<InvoicePrompt> prompts = [new("customer", "Customer name", Required: true),
            new("vehicle", "Year and make (optional)"), new("vin", "VIN (optional)")];
        if (Variant == "Calibration")
        {
            prompts.Add(new("claim", "Claim number (optional)"));
            prompts.Add(new("location", "Calibration location", "front-end", Choices: ["front-end", "rear-end", "side mirror"]));
            prompts.Add(new("calibration", "Calibration type", "static", Choices: ["static", "dynamic", "universal"]));
            prompts.Add(new("po", "PO number (optional)"));
            return prompts;
        }
        prompts.Add(new("stock", "Stock number (optional)"));
        prompts.Add(new("po", "PO number (optional)"));
        if (Variant == "Service")
        {
            prompts.Add(new("s0qty", "Labor hours", "1", Numeric: true, Positive: true));
            for (int i = 1; i < ServiceItems.Length; i++)
            {
                prompts.Add(new($"s{i}qty", $"{ServiceItems[i]} quantity", "0", Numeric: true));
                prompts.Add(new($"s{i}price", $"{ServiceItems[i]} unit price", "0", Numeric: true));
            }
            return prompts;
        }
        prompts.Add(new("count", "Number of line items", "1", Numeric: true, Positive: true));
        int count = int.TryParse(Values.GetValueOrDefault("count", "1"), out int n) ? n : 1;
        for (int i = 0; i < count; i++)
        {
            string prefix = $"Item {i + 1}: ";
            prompts.Add(new($"r{i}qty", prefix + "quantity", "1", Numeric: true, Positive: true));
            prompts.Add(new($"r{i}number", prefix + "item number (optional)"));
            prompts.Add(new($"r{i}description", prefix + "description", Required: true));
            prompts.Add(new($"r{i}price", prefix + "unit price", "0", Numeric: true));
            prompts.Add(new($"r{i}discount", prefix + "discount amount", "0", Numeric: true));
        }
        return prompts;
    }

    public string? Set(InvoicePrompt prompt, string input)
    {
        string value = input.Trim();
        if (prompt.Required && value.Length == 0) return "This field is required.";
        if (prompt.Choices is not null && !prompt.Choices.Contains(value)) return "Choose one of the listed options.";
        if (prompt.Numeric)
        {
            if (value.Length == 0) value = Value(prompt);
            if (!decimal.TryParse(value.Replace("$", "").Replace(",", ""), NumberStyles.Number,
                CultureInfo.InvariantCulture, out decimal number) || number < 0 || (prompt.Positive && number == 0))
                return prompt.Positive ? "Enter a number greater than zero." : "Enter zero or a positive number.";
            if (prompt.Key == "count" && (number != decimal.Truncate(number) || number > 1000))
                return "Enter a whole number between 1 and 1000.";
            value = number.ToString(CultureInfo.InvariantCulture);
            if (prompt.Key.EndsWith("discount"))
            {
                string prefix = prompt.Key[..^8];
                if (number > Number(prefix + "qty", "1") * Number(prefix + "price", "0"))
                    return "Discount cannot exceed the item amount.";
            }
        }
        Values[prompt.Key] = value.Replace("\\r\\n", "\n").Replace("\\n", "\n").Replace("\\r", "\n");
        return null;
    }

    private decimal Number(string key, string fallback) => decimal.Parse(Values.GetValueOrDefault(key, fallback), CultureInfo.InvariantCulture);
    public DocumentRecord Build(decimal laborRate)
    {
        foreach (var prompt in Prompts())
        {
            string? error = Set(prompt, Value(prompt));
            if (error is not null) throw new ArgumentException($"{prompt.Label}: {error}");
        }
        var document = new DocumentRecord
        {
            DocumentType = "Invoice", InvoiceVariant = Variant, CustomerName = Values["customer"],
            YearAndMake = Values["vehicle"], Vin = Values["vin"], PurchaseOrder = Values["po"],
            StockNumber = Values.GetValueOrDefault("stock", ""), ClaimNumber = Values.GetValueOrDefault("claim", ""),
            CalibrationLocation = Values.GetValueOrDefault("location", ""), CalibrationType = Values.GetValueOrDefault("calibration", "")
        };
        if (Variant == "Service")
            for (int i = 0; i < ServiceItems.Length; i++)
                document.Items.Add(new() { Description = ServiceItems[i], ItemNumber = i == 0 ? "LABOUR" : "",
                    Quantity = Number($"s{i}qty", "0"), UnitPrice = i == 0 ? laborRate : Number($"s{i}price", "0") });
        else if (Variant == "Regular")
            for (int i = 0; i < (int)Number("count", "1"); i++)
                document.Items.Add(new() { Quantity = Number($"r{i}qty", "1"), ItemNumber = Values[$"r{i}number"],
                    Description = Values[$"r{i}description"], UnitPrice = Number($"r{i}price", "0"), Discount = Number($"r{i}discount", "0") });
        InvoiceBuilder.Recalculate(document);
        return document;
    }
}
