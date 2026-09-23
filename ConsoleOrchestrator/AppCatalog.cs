namespace ConsoleOrchestrator;

public static class AppCatalog
{
    private static readonly string WorkspaceRoot = Path.GetFullPath(
        Path.Combine(AppContext.BaseDirectory, "../../../.."));

    public static IReadOnlyList<ConsoleAppDefinition> Apps { get; } =
    [
        new(
            "CollisionLink Downloader",
            "",
            WorkingDirectory: Path.Combine(WorkspaceRoot, "CollisionLinkDownloader")),
        new(
            "AutoRetrieval",
            "",
            WorkingDirectory: Path.Combine(WorkspaceRoot, "AutoRetrieval")),
        new(
            "Parts Order",
            "",
            WorkingDirectory: Path.Combine(WorkspaceRoot, "PartsOrder")),
        new("Invoice Creation", "", WorkingDirectory: Path.Combine(WorkspaceRoot, "InvoiceGenerator")),
        new("Quote Creation", "", WorkingDirectory: Path.Combine(WorkspaceRoot, "InvoiceGenerator")),
        new("Manage Quotes", "", WorkingDirectory: Path.Combine(WorkspaceRoot, "InvoiceGenerator")),
        new("Search Documents", "", WorkingDirectory: Path.Combine(WorkspaceRoot, "InvoiceGenerator"))
    ];
}

public sealed record ConsoleAppDefinition(
    string Name,
    string FileName,
    string Arguments = "",
    string WorkingDirectory = ".",
    bool Enabled = true,
    string[]? ArgumentList = null);
