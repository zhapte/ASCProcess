using ConsoleOrchestrator;
using Terminal.Gui.App;
using Terminal.Gui.Input;

// Add your console applications in AppCatalog.cs. No UI changes are needed.
using IApplication terminalApp = Application.Create();
terminalApp.Init();

// Handle the quit shortcut at application scope. The embedded terminal
// consumes most key events, so a focused terminal view can otherwise prevent
// Ctrl+Q from reaching the orchestrator.
terminalApp.Keyboard.KeyDown += (_, key) =>
{
    if (key.IsCtrl && key.NoCtrl == Key.Q)
    {
        key.Handled = true;
        terminalApp.RequestStop();
    }
};

using OrchestratorWindow window = new(terminalApp, AppCatalog.Apps);
terminalApp.Run(window);
