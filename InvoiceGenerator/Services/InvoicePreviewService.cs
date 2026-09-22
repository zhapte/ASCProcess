using InvoiceGenerator.Models;

namespace InvoiceGenerator.Services;

public static class InvoicePreviewService
{
    public static void Display(DocumentRecord document)
    {
        ConsoleUi.Clear();

        Console.WriteLine("==================================");
        Console.WriteLine($"          {GetTitle(document)} Preview");
        Console.WriteLine("==================================");
        Console.WriteLine();

        Console.WriteLine(
            $"Customer:       {document.CustomerName}"
        );

        Console.WriteLine(
            $"Year and make:  {document.YearAndMake}"
        );

        Console.WriteLine(
            $"VIN:            {document.Vin}"
        );

        if (IsCalibration(document))
        {
            Console.WriteLine(
                $"Claim number:   {document.ClaimNumber}"
            );
            Console.WriteLine(
                $"Location:       {document.CalibrationLocation}"
            );
            Console.WriteLine(
                $"Type:           {document.CalibrationType}"
            );
        }
        else
        {
            Console.WriteLine(
                $"Stock number:   {document.StockNumber}"
            );
        }

        Console.WriteLine(
            $"PO number:      {document.PurchaseOrder}"
        );

        Console.WriteLine();

        if (IsCalibration(document))
        {
            Console.WriteLine(
                $"Subtotal:       {document.Subtotal:C2}"
            );
            Console.WriteLine(
                $"GST 5%:         {document.Gst:C2}"
            );
            Console.WriteLine(
                $"PST 7%:         {document.Pst:C2}"
            );
            Console.WriteLine(
                $"Total:          {document.Total:C2}"
            );
            Console.WriteLine();
            return;
        }

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
    }

    private static string GetTitle(DocumentRecord document)
    {
        if (IsCalibration(document))
        {
            return "Calibration Invoice";
        }

        if (IsService(document))
        {
            return "Service Invoice";
        }

        return document.DocumentType;
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
