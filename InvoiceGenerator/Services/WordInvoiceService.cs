using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using InvoiceGenerator.Models;

namespace InvoiceGenerator.Services;

public class WordInvoiceService
{
    private const string HeaderBannerFill = "B8CCE4";
    private const int CompactItemFontSize = 18;
    private const uint CompactEmptyRowHeight = 80;
    private const uint CompactRowHeight = 160;
    private const uint CompactTotalRowHeight = 180;

    private static readonly string[] ItemPlaceholders =
    [
        "{{Q}}",
        "{{I}}",
        "{{DESC}}",
        "{{UP}}",
        "{{DISC}}",
        "{{LT}}"
    ];

    private static readonly string[] TotalPlaceholders =
    [
        "{{SUB}}",
        "{{GST}}",
        "{{PST}}",
        "{{TOTAL}}"
    ];

    public string Generate(DocumentRecord invoice)
    {
        if (string.IsNullOrWhiteSpace(invoice.DocumentNumber))
        {
            throw new InvalidOperationException(
                "The invoice must have a document number before generating Word."
            );
        }

        AppSettings settings =
            AppSettingsService.Load();

        string templatePath =
            AppSettingsService.ResolvePath(
                IsService(invoice)
                    ? settings.Templates.ServiceTemplatePath
                    : IsCalibration(invoice)
                    ? settings.Templates.CalibrationTemplatePath
                    : settings.Templates.InvoiceTemplatePath
            );

        string outputFolder =
            AppSettingsService.ResolvePath(
                settings.Storage.OutputFolder
            );

        Directory.CreateDirectory(outputFolder);

        string safeFileName = MakeSafeFileName(
            invoice.DocumentNumber
        );

        string outputFileName =
            IsCalibration(invoice)
                ? $"{safeFileName} CALIBRATION"
                : string.Equals(
                    invoice.DocumentType,
                    "Quote",
                    StringComparison.OrdinalIgnoreCase
                )
                ? $"Quote-{safeFileName}"
                : safeFileName;

        string outputPath = Path.Combine(
            outputFolder,
            $"{outputFileName}.docx"
        );

        CopyTemplateToOutput(
            templatePath,
            outputPath
        );

        using WordprocessingDocument document =
            WordprocessingDocument.Open(
                outputPath,
                true
            );

        MainDocumentPart mainDocumentPart =
            document.MainDocumentPart
            ?? throw new InvalidOperationException(
                "The Word document main part could not be found."
            );

        Document wordDocument =
            mainDocumentPart.Document
            ?? throw new InvalidOperationException(
                "The Word document content could not be found."
            );

        Dictionary<string, string> replacements = new()
        {
            ["{{INVOICE}}"] =
                invoice.DocumentNumber,

            ["{{DATE}}"] =
                invoice.CreatedDate.ToString("MMMM d, yyyy")
                    .ToUpperInvariant(),

            ["{{CUSTOMER}}"] =
                invoice.CustomerName,

            ["{{SOLD}}"] =
                invoice.CustomerName,

            ["{{YEAR}}"] =
                invoice.YearAndMake,

            ["{{VIN}}"] =
                invoice.Vin,

            ["{{S}}"] =
                invoice.StockNumber,

            ["{{C}}"] =
                invoice.ClaimNumber,

            ["{{LOCATION}}"] =
                invoice.CalibrationLocation,

            ["{{TYPE}}"] =
                invoice.CalibrationType,

            ["{{PO}}"] =
                invoice.PurchaseOrder,

            ["{{SUB}}"] =
                invoice.Subtotal.ToString("C2"),

            ["{{GST}}"] =
                invoice.Gst.ToString("C2"),

            ["{{PST}}"] =
                invoice.Pst.ToString("C2"),

            ["{{TOTAL}}"] =
                invoice.Total.ToString("C2")
        };

        EnsureHeaderBannerFallback(wordDocument);
        ReplaceDocumentTitle(wordDocument, invoice.DocumentType);
        if (IsService(invoice))
        {
            PopulateServiceRows(wordDocument, invoice.Items);
        }
        else if (!IsCalibration(invoice))
        {
            PopulateItemRows(wordDocument, invoice.Items);
        }
        ReplacePlaceholders(document, replacements);

        return outputPath;
    }

    private static void ReplaceDocumentTitle(
        Document wordDocument,
        string documentType)
    {
        string title =
            documentType.ToUpperInvariant();

        Paragraph? titleParagraph =
            wordDocument.Body?
                .Descendants<Paragraph>()
                .FirstOrDefault(paragraph =>
                    string.Equals(
                        string.Concat(
                            paragraph
                                .Descendants<Text>()
                                .Select(text => text.Text)
                        ).Trim(),
                        "INVOICE",
                        StringComparison.OrdinalIgnoreCase
                    ));

        if (titleParagraph is null)
        {
            return;
        }

        List<Text> textNodes =
            titleParagraph.Descendants<Text>().ToList();

        if (textNodes.Count == 0)
        {
            return;
        }

        textNodes[0].Text = title;

        for (int i = 1; i < textNodes.Count; i++)
        {
            textNodes[i].Text = string.Empty;
        }
    }

    private static void CopyTemplateToOutput(
        string templatePath,
        string outputPath)
    {
        if (!File.Exists(templatePath))
        {
            throw new FileNotFoundException(
                "The document template file could not be found. Check the Templates section in app.json.",
                templatePath
            );
        }

        File.Copy(
            templatePath,
            outputPath,
            overwrite: true
        );
    }

    private static void EnsureHeaderBannerFallback(
        Document wordDocument)
    {
        Table? headerTable =
            wordDocument.Body?
                .Elements<Table>()
                .FirstOrDefault();

        TableRow? headerRow =
            headerTable?
                .Elements<TableRow>()
                .FirstOrDefault();

        if (headerRow is null)
        {
            return;
        }

        foreach (TableCell cell in headerRow.Elements<TableCell>())
        {
            TableCellProperties properties =
                cell.GetFirstChild<TableCellProperties>()
                ?? cell.PrependChild(new TableCellProperties());

            Shading shading =
                properties.GetFirstChild<Shading>()
                ?? properties.AppendChild(new Shading());

            shading.Val = ShadingPatternValues.Clear;
            shading.Color = "auto";
            shading.Fill = HeaderBannerFill;
        }
    }

    private static void PopulateItemRows(
        Document wordDocument,
        List<InvoiceItem> items)
    {
        Body body =
            wordDocument.Body
            ?? throw new InvalidOperationException(
                "The Word document body could not be found."
            );

        TableRow templateRow =
            body.Descendants<TableRow>()
                .FirstOrDefault(ContainsItemPlaceholder)
            ?? throw new InvalidOperationException(
                "The invoice item placeholder row could not be found."
            );

        Table table =
            templateRow.Ancestors<Table>().FirstOrDefault()
            ?? throw new InvalidOperationException(
                "The invoice item table could not be found."
            );

        List<TableRow> tableRows =
            table.Elements<TableRow>().ToList();

        int templateRowIndex =
            tableRows.IndexOf(templateRow);

        int totalsRowIndex =
            tableRows.FindIndex(
                templateRowIndex + 1,
                ContainsTotalPlaceholder
            );

        if (totalsRowIndex < 0)
        {
            throw new InvalidOperationException(
                "The invoice totals row could not be found."
            );
        }

        TableRow totalsRow = tableRows[totalsRowIndex];

        TableRow extraItemTemplateRow =
            (TableRow)templateRow.CloneNode(true);

        List<TableRow> existingItemRows =
            tableRows
                .Skip(templateRowIndex)
                .Take(totalsRowIndex - templateRowIndex)
                .ToList();

        for (int i = 0; i < existingItemRows.Count; i++)
        {
            TableRow replacementRow =
                (TableRow)extraItemTemplateRow.CloneNode(true);

            if (i < items.Count)
            {
                WriteItemToRow(
                    replacementRow,
                    items[i]
                );
            }
            else
            {
                ClearRowText(replacementRow);
            }

            existingItemRows[i].InsertBeforeSelf(replacementRow);
            existingItemRows[i].Remove();
        }

        for (int i = existingItemRows.Count; i < items.Count; i++)
        {
            TableRow itemRow =
                (TableRow)extraItemTemplateRow.CloneNode(true);

            WriteItemToRow(
                itemRow,
                items[i]
            );

            table.InsertBefore(
                itemRow,
                totalsRow
            );
        }

        ApplyCompactTableFormatting(
            table,
            templateRowIndex
        );
    }

    private static void WriteItemToRow(
        TableRow row,
        InvoiceItem item)
    {
        List<TableCell> cells =
            row.Elements<TableCell>().ToList();

        if (cells.Count < 6)
        {
            throw new InvalidOperationException(
                "The invoice item row must have at least six cells."
            );
        }

        SetCellText(
            cells[0],
            item.Quantity.ToString("0.##")
        );

        SetCellText(
            cells[1],
            item.ItemNumber
        );

        SetCellText(
            cells[2],
            item.Description
        );

        SetCellText(
            cells[3],
            item.UnitPrice.ToString("C2")
        );

        SetCellText(
            cells[4],
            item.Discount.ToString("C2")
        );

        SetCellText(
            cells[5],
            item.LineTotal.ToString("C2")
        );
    }

    private static void PopulateServiceRows(
        Document wordDocument,
        List<InvoiceItem> items)
    {
        Body body =
            wordDocument.Body
            ?? throw new InvalidOperationException(
                "The Word document body could not be found."
            );

        TableRow firstServiceRow =
            body.Descendants<TableRow>()
                .FirstOrDefault(row =>
                    GetRowText(row).Contains(
                        "MAINTENANCE SERVICE",
                        StringComparison.OrdinalIgnoreCase
                    ))
            ?? throw new InvalidOperationException(
                "The service item row could not be found."
            );

        Table table =
            firstServiceRow.Ancestors<Table>().FirstOrDefault()
            ?? throw new InvalidOperationException(
                "The service item table could not be found."
            );

        List<TableRow> rows =
            table.Elements<TableRow>().ToList();

        int firstServiceRowIndex =
            rows.IndexOf(firstServiceRow);

        int totalsRowIndex =
            rows.FindIndex(
                firstServiceRowIndex + 1,
                ContainsTotalPlaceholder
            );

        if (totalsRowIndex < 0)
        {
            throw new InvalidOperationException(
                "The service totals row could not be found."
            );
        }

        List<TableRow> serviceRows =
            rows
                .Skip(firstServiceRowIndex)
                .Take(totalsRowIndex - firstServiceRowIndex)
                .ToList();

        for (int i = 0; i < serviceRows.Count; i++)
        {
            InvoiceItem? item =
                i < items.Count ? items[i] : null;

            WriteServiceItemToRow(
                serviceRows[i],
                item
            );
        }

        ApplyCompactTableFormatting(
            table,
            firstServiceRowIndex
        );
    }

    private static void WriteServiceItemToRow(
        TableRow row,
        InvoiceItem? item)
    {
        List<TableCell> cells =
            row.Elements<TableCell>().ToList();

        if (cells.Count < 6)
        {
            throw new InvalidOperationException(
                "The service item row must have at least six cells."
            );
        }

        if (item is null ||
            item.Quantity == 0 ||
            item.UnitPrice == 0)
        {
            SetCellText(cells[0], string.Empty);
            SetCellText(cells[1], string.Empty);
            SetCellText(cells[3], string.Empty);
            SetCellText(cells[4], string.Empty);
            SetCellText(cells[5], string.Empty);
            return;
        }

        SetCellText(
            cells[0],
            item.Quantity.ToString("0.##")
        );

        SetCellText(
            cells[1],
            item.ItemNumber
        );

        SetCellText(
            cells[3],
            item.UnitPrice.ToString("C2")
        );

        SetCellText(cells[4], string.Empty);

        SetCellText(
            cells[5],
            item.LineTotal.ToString("C2")
        );
    }

    private static void SetCellText(
        TableCell cell,
        string value)
    {
        List<Text> textNodes =
            cell.Descendants<Text>().ToList();

        if (textNodes.Count == 0)
        {
            Paragraph paragraph = new();
            Run run = new();

            AppendTextWithLineBreaks(
                run,
                value
            );

            paragraph.Append(run);
            cell.AppendChild(paragraph);

            return;
        }

        SetTextWithLineBreaks(
            textNodes[0],
            value
        );

        for (int i = 1; i < textNodes.Count; i++)
        {
            textNodes[i].Text = string.Empty;
        }
    }

    private static bool ContainsItemPlaceholder(
        TableRow row)
    {
        string rowText =
            GetRowText(row);

        return ItemPlaceholders.Any(
            placeholder => rowText.Contains(
                placeholder,
                StringComparison.OrdinalIgnoreCase
            )
        );
    }

    private static bool ContainsTotalPlaceholder(
        TableRow row)
    {
        string rowText =
            GetRowText(row);

        return TotalPlaceholders.Any(
            placeholder => rowText.Contains(
                placeholder,
                StringComparison.OrdinalIgnoreCase
            )
        );
    }

    private static string GetRowText(TableRow row)
    {
        return string.Concat(
            row.Descendants<Text>().Select(text => text.Text)
        );
    }

    private static void ClearRowText(TableRow row)
    {
        foreach (Text text in row.Descendants<Text>())
        {
            text.Text = string.Empty;
        }
    }

    private static void ApplyCompactTableFormatting(
        Table table,
        int firstItemRowIndex)
    {
        List<TableRow> rows =
            table.Elements<TableRow>().ToList();

        int totalsRowIndex =
            rows.FindIndex(
                firstItemRowIndex + 1,
                ContainsTotalPlaceholder
            );

        for (int i = 0; i < rows.Count; i++)
        {
            bool isEmptyItemRow =
                i >= firstItemRowIndex &&
                (totalsRowIndex < 0 || i < totalsRowIndex) &&
                string.IsNullOrWhiteSpace(GetRowText(rows[i]));

            bool isTotalRow =
                totalsRowIndex >= 0 &&
                i >= totalsRowIndex;

            ApplyCompactRowFormatting(
                rows[i],
                isEmptyItemRow,
                isTotalRow
            );
        }
    }

    private static void ApplyCompactRowFormatting(
        TableRow row,
        bool isEmptyItemRow,
        bool isTotalRow)
    {
        TableRowProperties rowProperties =
            row.GetFirstChild<TableRowProperties>()
            ?? row.PrependChild(new TableRowProperties());

        TableRowHeight rowHeight =
            rowProperties.GetFirstChild<TableRowHeight>()
            ?? rowProperties.AppendChild(new TableRowHeight());

        rowHeight.Val =
            isEmptyItemRow
                ? CompactEmptyRowHeight
                : isTotalRow
                    ? CompactTotalRowHeight
                    : CompactRowHeight;

        rowHeight.HeightType =
            isEmptyItemRow
                ? HeightRuleValues.Exact
                : HeightRuleValues.AtLeast;

        foreach (Paragraph paragraph in row.Descendants<Paragraph>())
        {
            ParagraphProperties properties =
                paragraph.ParagraphProperties
                ?? paragraph.PrependChild(new ParagraphProperties());

            properties.SpacingBetweenLines =
                new SpacingBetweenLines
                {
                    Before = "0",
                    After = "0",
                    Line = "200",
                    LineRule = LineSpacingRuleValues.Auto
                };
        }

        foreach (Run run in row.Descendants<Run>())
        {
            RunProperties properties =
                run.RunProperties
                ?? run.PrependChild(new RunProperties());

            properties.FontSize =
                new FontSize
                {
                    Val = CompactItemFontSize.ToString()
                };
        }
    }

    private static Dictionary<string, string> CreateItemReplacements(
        InvoiceItem item)
    {
        return new Dictionary<string, string>
        {
            ["{{Q}}"] =
                item.Quantity.ToString("0.##"),

            ["{{I}}"] =
                item.ItemNumber,

            ["{{DESC}}"] =
                item.Description,

            ["{{UP}}"] =
                item.UnitPrice.ToString("C2"),

            ["{{DISC}}"] =
                item.Discount.ToString("C2"),

            ["{{LT}}"] =
                item.LineTotal.ToString("C2")
        };
    }

    private static void ReplacePlaceholdersInElement(
        OpenXmlElement element,
        Dictionary<string, string> replacements)
    {
        foreach (Paragraph paragraph in
                 element.Descendants<Paragraph>())
        {
            ReplaceInParagraph(
                paragraph,
                replacements
            );
        }
    }

    private static void ReplacePlaceholders(
        WordprocessingDocument document,
        Dictionary<string, string> replacements)
    {
        MainDocumentPart mainDocumentPart =
            document.MainDocumentPart
            ?? throw new InvalidOperationException(
                "The Word document main part could not be found."
            );

        Document wordDocument =
            mainDocumentPart.Document
            ?? throw new InvalidOperationException(
                "The Word document content could not be found."
            );

        Body? body = wordDocument.Body;

        if (body is null)
        {
            throw new InvalidOperationException(
                "The Word document body could not be found."
            );
        }

        foreach (Paragraph paragraph in
                 body.Descendants<Paragraph>())
        {
            ReplaceInParagraph(
                paragraph,
                replacements
            );
        }
    }

    private static void ReplaceInParagraph(
        Paragraph paragraph,
        Dictionary<string, string> replacements)
    {
        List<Text> textNodes =
            paragraph.Descendants<Text>().ToList();

        if (textNodes.Count == 0)
        {
            return;
        }

        string originalText =
            string.Concat(
                textNodes.Select(text => text.Text)
            );

        string updatedText = originalText;

        foreach (
            KeyValuePair<string, string> replacement
            in replacements)
        {
            updatedText = updatedText.Replace(
                replacement.Key,
                replacement.Value,
                StringComparison.OrdinalIgnoreCase
            );
        }

        if (updatedText == originalText)
        {
            return;
        }

        SetTextWithLineBreaks(
            textNodes[0],
            updatedText
        );

        for (int i = 1; i < textNodes.Count; i++)
        {
            textNodes[i].Text = string.Empty;
        }
    }

    private static void SetTextWithLineBreaks(
        Text firstText,
        string value)
    {
        string[] lines =
            NormalizeLineEndings(value).Split('\n');

        firstText.Text = lines[0];
        firstText.Space = SpaceProcessingModeValues.Preserve;

        OpenXmlElement? parent =
            firstText.Parent;

        if (parent is null)
        {
            return;
        }

        OpenXmlElement insertAfter = firstText;

        for (int i = 1; i < lines.Length; i++)
        {
            Break lineBreak = new();
            insertAfter =
                parent.InsertAfter(lineBreak, insertAfter)
                ?? insertAfter;

            Text text = new(lines[i])
            {
                Space = SpaceProcessingModeValues.Preserve
            };

            insertAfter =
                parent.InsertAfter(text, insertAfter)
                ?? insertAfter;
        }
    }

    private static void AppendTextWithLineBreaks(
        Run run,
        string value)
    {
        string[] lines =
            NormalizeLineEndings(value).Split('\n');

        for (int i = 0; i < lines.Length; i++)
        {
            if (i > 0)
            {
                run.AppendChild(new Break());
            }

            run.AppendChild(
                new Text(lines[i])
                {
                    Space = SpaceProcessingModeValues.Preserve
                }
            );
        }
    }

    private static string NormalizeLineEndings(string value)
    {
        return value
            .Replace("\r\n", "\n")
            .Replace("\r", "\n");
    }

    private static string MakeSafeFileName(
        string fileName)
    {
        foreach (
            char invalidCharacter
            in Path.GetInvalidFileNameChars())
        {
            fileName = fileName.Replace(
                invalidCharacter,
                '_'
            );
        }

        return fileName;
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
}
