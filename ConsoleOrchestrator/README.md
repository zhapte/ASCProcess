# Console Orchestrator

A mouse- and keyboard-friendly Terminal.Gui dashboard for running several console applications from one terminal.

## Configure your applications

Edit `AppCatalog.cs`. Each entry needs a display name, executable, arguments, and working directory:

```csharp
new(
    Name: "Orders API",
    FileName: "dotnet",
    Arguments: "run --project ../Orders/Orders.csproj",
    WorkingDirectory: ".",
    Enabled: true)
```

`WorkingDirectory` may be absolute or relative to the directory from which you run the orchestrator. Set `Enabled: true` after replacing a placeholder with a real command.

## Run

```bash
dotnet run
```

The entries run the `CollisionLinkDownloader`, `InvoiceGenerator`, and `AutoRetrieval` source projects using the .NET SDK. Invoice runs through the embedded PTY; no separate `Invoice/` deployment is required. Its project copies settings and templates into its build output, and the configured database remains on the NAS. Preserve any local data in the old deployment folder before removing it.

Click an application in the left panel, then use **Start**, **Stop**, or **Restart**. Standard output and standard error appear in the log panel. When a program asks a question, type the response (for example, a claim number) in **Process input** and click **Send** or press Enter. Stopping an app terminates its whole child-process tree. Press `Esc` to exit; running child apps are stopped during shutdown.
