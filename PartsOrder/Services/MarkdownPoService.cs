using System.Text;

namespace PartsOrder.Services;

public class MarkdownPoService
{
    private readonly string _saveDirectory;

    public MarkdownPoService(string saveDirectory)
    {
        _saveDirectory = saveDirectory;
    }

    public async Task<string> CreateAsync(
        string poNumber,
        string orderDate,
        IEnumerable<PartEntry> parts,
        bool overwrite = true)
    {
        ValidatePoNumber(poNumber);

        Directory.CreateDirectory(_saveDirectory);

        string outputPath =
            Path.Combine(
                _saveDirectory,
                $"{poNumber}.md");

        var markdown = new StringBuilder();

        markdown.AppendLine($"PO: {poNumber}");
        markdown.AppendLine($"Order Date: {orderDate}");
        markdown.AppendLine();

        List<PartEntry> partList = parts.ToList();

        for (int i = 0; i < partList.Count; i++)
        {
            PartEntry part = partList[i];

            markdown.AppendLine($"Part: {part.Part}");
            markdown.AppendLine($"Part#: {part.PartNumber}");
            markdown.AppendLine($"Price: {part.Price}");
            markdown.AppendLine($"QTY: {part.Quantity}");
            markdown.AppendLine($"Supplier: {part.Supplier}");
            markdown.AppendLine($"Reason: {part.Reason}");

            if (i < partList.Count - 1)
            {
                markdown.AppendLine();
            }
        }

        await using var stream = new FileStream(outputPath,
            overwrite ? FileMode.Create : FileMode.CreateNew, FileAccess.Write, FileShare.None,
            4096, useAsync: true);
        await using var writer = new StreamWriter(stream);
        await writer.WriteAsync(markdown.ToString());

        return outputPath;
    }

    private static void ValidatePoNumber(string poNumber)
    {
        if (string.IsNullOrWhiteSpace(poNumber))
        {
            throw new ArgumentException(
                "A PO number is required.");
        }

        char[] invalidCharacters =
            Path.GetInvalidFileNameChars();

        if (poNumber.IndexOfAny(invalidCharacters) >= 0)
        {
            throw new ArgumentException(
                "The PO number contains characters " +
                "that cannot be used in a filename.");
        }
    }
}


public record PartEntry(
    string Part,
    string PartNumber,
    string Price,
    string Quantity,
    string Supplier,
    string Reason);
