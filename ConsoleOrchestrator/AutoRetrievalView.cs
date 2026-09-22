using AutoRetrieval.Services;
using Terminal.Gui.App;
using Terminal.Gui.Drawing;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

namespace ConsoleOrchestrator;

#pragma warning disable CS0618
public sealed class AutoRetrievalView : View
{
    private readonly IApplication _application;
    private readonly string _envPath;
    private readonly TextField _claim = new() { X = 16, Y = 0, Width = Dim.Fill() - 1, MouseHighlightStates = MouseState.None };
    private readonly TextField _registration = new() { X = 16, Y = 2, Width = Dim.Fill() - 1, MouseHighlightStates = MouseState.None };
    private readonly Button _start = new() { Text = "Start retrieval", X = 0, Y = 4 };
    private readonly Button _stop = new() { Text = "Stop", X = 21, Y = 4, Enabled = false };
    private readonly Button _reset = new() { Text = "Reset", Y = 4, MouseHighlightStates = MouseState.None };
    private readonly TextView _log = new() { X = 0, Y = 0, Width = Dim.Fill(), Height = Dim.Fill(), ReadOnly = true, WordWrap = true, MouseHighlightStates = MouseState.None };
    private CancellationTokenSource? _cancellation;
    private bool _disposed;

    public AutoRetrievalView(IApplication application, string envPath)
    {
        _application = application;
        _envPath = envPath;
        CanFocus = true;
        _stop.X = Pos.Right(_start) + 1;
        _reset.X = Pos.Right(_stop) + 1;
        Add(new Label { Text = "Claim number", X = 0, Y = 0 }, _claim,
            new Label { Text = "Registration", X = 0, Y = 2 }, _registration,
            _start, _stop, _reset);
        var consolePanel = new FrameView { Title = "Console output", X = 0, Y = 6, Width = Dim.Fill(), Height = Dim.Fill(), MouseHighlightStates = MouseState.None };
        var outputColor = new Terminal.Gui.Drawing.Attribute(ColorName16.White, ColorName16.DarkGray);
        // Read-only text has its own role; leaving it derived dims the text
        // against the inherited background even when Normal is readable.
        _log.SetScheme(new Scheme(outputColor)
        {
            ReadOnly = outputColor,
            Editable = outputColor,
            Focus = outputColor,
            Active = outputColor,
            Highlight = new Terminal.Gui.Drawing.Attribute(ColorName16.Black, ColorName16.BrightCyan),
            Disabled = outputColor
        });
        consolePanel.SetScheme(new Scheme(outputColor)
        {
            Focus = outputColor,
            Active = outputColor
        });
        consolePanel.Add(_log);
        Add(consolePanel);
        _log.Text = "Claim: AB12345-6 or AB123456 (-A added automatically). Registration: 8 digits.\nChromium stays open after the workflow for review.\n";
        _reset.Accepted += (_, _) =>
        {
            if (_cancellation is not null) return;
            _claim.Text = string.Empty;
            _registration.Text = string.Empty;
            _log.Text = string.Empty;
            _claim.SetFocus();
        };
        _start.Accepted += async (_, _) => await StartAsync();
        _stop.Accepted += (_, _) => { Log("Stopping workflow and closing browser..."); _cancellation?.Cancel(); };
    }

    private void Log(string message)
    {
        if (_disposed) return;
        _application.Invoke(() =>
        {
            if (_disposed) return;
            string text = _log.Text + $"[{DateTime.Now:HH:mm:ss}] {message}" + Environment.NewLine;
            _log.Text = text.Length > 50000 ? text[^50000..] : text;
            _log.MoveEnd();
        });
    }

    private async Task StartAsync()
    {
        if (_cancellation is not null) return;
        if (!RetrievalRequest.TryNormalizeClaim(_claim.Text, out string claim))
        { Log("Enter a claim like AB12345-6 or AB123456."); _claim.SetFocus(); return; }
        string registration = _registration.Text.Trim();
        if (!RetrievalRequest.IsValidRegistration(registration))
        { Log("Registration must contain exactly eight digits."); _registration.SetFocus(); return; }
        _claim.Text = claim;
        _registration.Text = registration;
        _log.Text = string.Empty;
        Log($"Starting retrieval for {claim}.");
        _claim.Enabled = _registration.Enabled = _start.Enabled = false;
        _stop.Enabled = true;
        _reset.Enabled = false;
        using var cancellation = new CancellationTokenSource();
        _cancellation = cancellation;
        try
        {
            await Task.Run(() => new RetrievalWorkflow().RunAsync(
                new(claim, registration), _envPath, Log, cancellation.Token));
            Log("Browser session ended.");
        }
        catch (Exception ex) { Log(cancellation.IsCancellationRequested ? "Stopped." : $"Error: {ex.Message}"); }
        finally
        {
            _cancellation = null;
            if (!_disposed) _application.Invoke(() =>
            {
                if (_disposed) return;
                _claim.Enabled = _registration.Enabled = _start.Enabled = true;
                _stop.Enabled = false;
                _reset.Enabled = true;
            });
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) { _disposed = true; _cancellation?.Cancel(); }
        base.Dispose(disposing);
    }
}
#pragma warning restore CS0618
