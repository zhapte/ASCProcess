using PartsOrder.Services;

Console.Title = "Markdown PO Generator";

string homeDirectory =
    Environment.GetFolderPath(
        Environment.SpecialFolder.UserProfile);

string saveDirectory =
    Path.Combine(
        homeDirectory,
        "NAS",
        "Parts Ordered by Jason",
        "Order Mark Down");

if (!Console.IsOutputRedirected) Console.Clear();

Console.WriteLine("======================================");
Console.WriteLine("       Markdown PO Generator");
Console.WriteLine("======================================");
Console.WriteLine();


// ===========================
// Check Save Location
// ===========================

string partsDirectory =
    Path.Combine(
        homeDirectory,
        "NAS",
        "Parts Ordered by Jason");

if (!Directory.Exists(partsDirectory))
{
    Console.WriteLine("ERROR: Parts folder is not available.");
    Console.WriteLine();
    Console.WriteLine(partsDirectory);
    Console.WriteLine();
    Console.WriteLine(
        "Make sure the NAS is mounted and try again.");

    Pause();
    return;
}

try
{
    Directory.CreateDirectory(saveDirectory);
}
catch (Exception ex)
{
    Console.WriteLine(
        "ERROR: The save folder could not be created or accessed:");

    Console.WriteLine(saveDirectory);
    Console.WriteLine();
    Console.WriteLine(ex.Message);

    Pause();
    return;
}


// ===========================
// Number of Parts
// ===========================

int partCount;

while (true)
{
    Console.Write(
        "How many parts are being ordered? (leave blank for 1): ");

    string? input = Console.ReadLine();

    if (string.IsNullOrWhiteSpace(input))
    {
        partCount = 1;
        break;
    }

    if (int.TryParse(input, out partCount) &&
        partCount > 0)
    {
        break;
    }

    Console.WriteLine();
    Console.WriteLine(
        "ERROR: Enter a whole number greater than 0.");
    Console.WriteLine();
}


// ===========================
// PO Number
// ===========================

string poNumber;

while (true)
{
    Console.Write("PO Number: ");

    poNumber = Console.ReadLine()?.Trim() ?? "";

    if (!string.IsNullOrWhiteSpace(poNumber))
    {
        break;
    }

    Console.WriteLine();
    Console.WriteLine("ERROR: A PO number is required.");
    Console.WriteLine();
}


// ===========================
// Order Date
// ===========================

Console.Write("Order Date (leave blank for today): ");

string? orderDateInput = Console.ReadLine();

string orderDate =
    string.IsNullOrWhiteSpace(orderDateInput)
        ? DateTime.Today.ToShortDateString()
        : orderDateInput.Trim();

Console.WriteLine();
Console.WriteLine($"Using order date: {orderDate}");


// ===========================
// Parts
// ===========================

var parts = new List<PartEntry>();

for (int i = 1; i <= partCount; i++)
{
    Console.WriteLine();
    Console.WriteLine("======================================");
    Console.WriteLine($"Part {i} of {partCount}");
    Console.WriteLine("======================================");

    Console.Write("Part: ");
    string part = ReadOrDefault("N/A");

    Console.Write("Part#: ");
    string partNumber = ReadOrDefault("N/A");

    Console.Write("Price: ");
    string price = ReadOrDefault("N/A");

    Console.Write("QTY: ");
    string quantity = ReadOrDefault("1");

    Console.Write("Supplier: ");
    string supplier = ReadOrDefault("N/A");

    Console.Write("Reason: ");
    string reason = ReadOrDefault("N/A");

    parts.Add(
        new PartEntry(
            part,
            partNumber,
            price,
            quantity,
            supplier,
            reason));
}


// ===========================
// Create Markdown
// ===========================

var markdownPoService =
    new MarkdownPoService(saveDirectory);

try
{
    string outputPath =
        await markdownPoService.CreateAsync(
            poNumber,
            orderDate,
            parts);

    Console.WriteLine();
    Console.WriteLine("======================================");
    Console.WriteLine("Markdown file created successfully!");
    Console.WriteLine();
    Console.WriteLine(outputPath);
    Console.WriteLine("======================================");
}
catch (Exception ex)
{
    Console.WriteLine();
    Console.WriteLine(
        "ERROR: The Markdown file could not be created.");

    Console.WriteLine();
    Console.WriteLine(ex.Message);
}

Console.WriteLine();

Pause();


// ===========================
// Helpers
// ===========================

static string ReadOrDefault(string defaultValue)
{
    string? input = Console.ReadLine();

    return string.IsNullOrWhiteSpace(input)
        ? defaultValue
        : input.Trim();
}

static void Pause()
{
    Console.WriteLine();
    if (Console.IsInputRedirected)
    {
        Console.WriteLine("Press Enter to continue...");
        Console.ReadLine();
        return;
    }
    Console.WriteLine("Press any key to continue...");
    Console.ReadKey(true);
}
