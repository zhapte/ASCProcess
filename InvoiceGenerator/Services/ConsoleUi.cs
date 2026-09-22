namespace InvoiceGenerator.Services;

public static class ConsoleUi
{
    public const string ClearMarker = "\u001eASC_CLEAR\u001e";

    public static void Clear()
    {
        if (Console.IsOutputRedirected)
        {
            Console.Write(ClearMarker);
            Console.Out.Flush();
            return;
        }

        Console.Clear();
    }
}
