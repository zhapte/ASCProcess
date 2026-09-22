using System.Runtime.InteropServices;

namespace InvoiceGenerator.Services;

public class PdfService
{
    private const int WdExportFormatPdf = 17;
    private const int WdDoNotSaveChanges = 0;

    public bool IsAvailable()
    {
        if (!OperatingSystem.IsWindows())
        {
            return false;
        }

        Type? wordType = Type.GetTypeFromProgID(
            "Word.Application"
        );

        return wordType is not null;
    }

    public string ExportToPdf(string wordFilePath)
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException(
                "PDF export through Microsoft Word is only available on Windows."
            );
        }

        if (!File.Exists(wordFilePath))
        {
            throw new FileNotFoundException(
                "The Word file could not be found.",
                wordFilePath
            );
        }

        Type wordType =
            Type.GetTypeFromProgID("Word.Application")
            ?? throw new InvalidOperationException(
                "Microsoft Word is not installed or is not registered for automation."
            );

        string pdfFilePath =
            Path.ChangeExtension(wordFilePath, ".pdf");

        object? wordApplication = null;
        object? document = null;

        try
        {
            wordApplication =
                Activator.CreateInstance(wordType)
                ?? throw new InvalidOperationException(
                    "Microsoft Word could not be started."
                );

            InvokeSetProperty(
                wordApplication,
                "Visible",
                false
            );

            object documents =
                InvokeGetProperty(
                    wordApplication,
                    "Documents"
                );

            document = InvokeMethod(
                documents,
                "Open",
                wordFilePath,
                false,
                true
            );

            InvokeMethod(
                document,
                "ExportAsFixedFormat",
                pdfFilePath,
                WdExportFormatPdf
            );

            InvokeMethod(
                document,
                "Close",
                WdDoNotSaveChanges
            );

            document = null;

            InvokeMethod(
                wordApplication,
                "Quit",
                WdDoNotSaveChanges
            );

            wordApplication = null;

            if (!File.Exists(pdfFilePath))
            {
                throw new InvalidOperationException(
                    "Microsoft Word did not create the expected PDF file."
                );
            }

            return pdfFilePath;
        }
        finally
        {
            CloseComObject(document, "Close");
            CloseComObject(wordApplication, "Quit");
        }
    }

    private static object InvokeGetProperty(
        object target,
        string propertyName)
    {
        return target.GetType().InvokeMember(
            propertyName,
            System.Reflection.BindingFlags.GetProperty,
            binder: null,
            target,
            args: null
        )
        ?? throw new InvalidOperationException(
            $"Microsoft Word returned no value for '{propertyName}'."
        );
    }

    private static void InvokeSetProperty(
        object target,
        string propertyName,
        object value)
    {
        target.GetType().InvokeMember(
            propertyName,
            System.Reflection.BindingFlags.SetProperty,
            binder: null,
            target,
            args: [value]
        );
    }

    private static object InvokeMethod(
        object target,
        string methodName,
        params object[] args)
    {
        return target.GetType().InvokeMember(
            methodName,
            System.Reflection.BindingFlags.InvokeMethod,
            binder: null,
            target,
            args
        )
        ?? new object();
    }

    private static void CloseComObject(
        object? target,
        string closeMethod)
    {
        if (target is null)
        {
            return;
        }

        try
        {
            InvokeMethod(
                target,
                closeMethod,
                WdDoNotSaveChanges
            );
        }
        catch
        {
            // Best effort cleanup. Preserve the original export error.
        }

        if (OperatingSystem.IsWindows() &&
            Marshal.IsComObject(target))
        {
            Marshal.FinalReleaseComObject(target);
        }
    }
}
