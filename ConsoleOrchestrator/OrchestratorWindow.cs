using System.Text;
using Terminal.Gui.App;
using Terminal.Gui.Drawing;
using Terminal.Gui.Input;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

namespace ConsoleOrchestrator;

#pragma warning disable CS0618 // TextView remains appropriate for a built-in, read-only log pane.
public sealed class OrchestratorWindow : Window
{
    private const int MaxLogCharacters = 100_000;
    private readonly IApplication _application;
    private readonly List<ManagedConsoleApp> _apps;
    private readonly Dictionary<ManagedConsoleApp, StringBuilder> _logs = [];
    private readonly Label _nameLabel;
    private readonly Label _commandLabel;
    private readonly Label _statusLabel;
    private readonly TextView _outputView;
    private readonly Button _startButton;
    private readonly Button _stopButton;
    private readonly Button _restartButton;
    private readonly TextField _inputField;
    private readonly Button _sendButton;
    private readonly MenuBar _invoiceMenu;
    private readonly Label _inputLabel;
    private readonly Button _clearButton;
    private readonly PtyTerminalSession _invoiceTerminal;
    private readonly TerminalView _terminalView;
    private readonly FrameView _sidebar;
    private readonly Label _sidebarHeader;
    private readonly Dictionary<ManagedConsoleApp, Button> _navigationButtons = [];
    private readonly Button _optionsButton;
    private readonly PartsOrderView _partsOrderView;
    private readonly AutoRetrievalView _autoRetrievalView;
    private readonly CollisionDownloadView _collisionDownloadView;
    private readonly InvoiceCreationView _invoiceCreationView;
    private readonly InvoiceCreationView _quoteCreationView;
    private readonly QuoteManagementView _quoteManagementView;
    private readonly DocumentSearchView _documentSearchView;
    private readonly InvoiceOptionsView _optionsView;
    private readonly FrameView _workspace;
    private ManagedConsoleApp? _selected;

    public OrchestratorWindow(IApplication application, IReadOnlyList<ConsoleAppDefinition> definitions)
    {
        _application = application;
        _apps = definitions.Select(definition => new ManagedConsoleApp(definition)).ToList();
        Title = " 🧭 ASC PROCESS HUB  •  real embedded terminal  •  Ctrl+Q quits ";
        SetScheme(CreateDashboardScheme());

        FrameView sidebar = new() { Title = "Navigation", X = 0, Y = 0, Width = 30, Height = Dim.Fill() };
        _sidebar = sidebar;
        sidebar.SetScheme(CreateSidebarScheme());
        _sidebarHeader = new Label { Text = "🔌 CONNECTED TOOLS", X = 2, Y = 0, Width = Dim.Fill() - 3 };
        sidebar.Add(_sidebarHeader);

        for (int index = 0; index < _apps.Count; index++)
        {
            ManagedConsoleApp managedApp = _apps[index];
            Button appButton = new()
            {
                Text = GetNavigationLabel(managedApp.Definition),
                X = 1,
                Y = 2 + index * 2,
                Width = Dim.Fill() - 2,
                Height = 2
            };
            appButton.SetScheme(CreateNavigationButtonScheme());
            appButton.Accepted += (_, _) => SelectApp(managedApp);
            sidebar.Add(appButton);
            _navigationButtons[managedApp] = appButton;
            _logs[managedApp] = new StringBuilder();
            managedApp.OutputReceived += line => OnOutput(managedApp, line);
            managedApp.StateChanged += () => _application.Invoke(RefreshSelectedApp);
        }

        FrameView details = new()
        {
            Title = "Workspace",
            X = Pos.Right(sidebar),
            Y = 0,
            Width = Dim.Fill(),
            Height = Dim.Fill()
        };
        details.SetScheme(CreatePanelScheme());
        _workspace = details;

        _nameLabel = new Label { X = 2, Y = 0, Width = Dim.Fill() - 3 };
        _commandLabel = new Label { X = 2, Y = 1, Width = Dim.Fill() - 3 };
        _statusLabel = new Label { X = 2, Y = 2, Width = Dim.Fill() - 3 };
        _startButton = new Button { Text = "▶ _Start", X = 1, Y = 4 };
        _stopButton = new Button { Text = "■ S_top", X = Pos.Right(_startButton) + 1, Y = 4 };
        _restartButton = new Button { Text = "↻ _Restart", X = Pos.Right(_stopButton) + 1, Y = 4 };
        _invoiceMenu = CreateInvoiceMenu();
        _invoiceMenu.X = 1;
        _invoiceMenu.Y = 6;
        _invoiceMenu.Width = 25;
        _clearButton = new Button { Text = "_Clear", X = 28, Y = 6, Height = 2 };
        _startButton.SetScheme(CreateActionScheme(ColorName16.BrightGreen));
        _stopButton.SetScheme(CreateActionScheme(ColorName16.BrightRed));
        _restartButton.SetScheme(CreateActionScheme(ColorName16.BrightYellow));
        _clearButton.SetScheme(CreateActionScheme(ColorName16.BrightCyan));

        _inputLabel = new Label { Text = "⌨  TYPE A RESPONSE • BLANK IS ALLOWED", X = 1, Y = 8 };
        _inputField = new TextField { X = 1, Y = 9, Width = Dim.Fill() - 13 };
        _sendButton = new Button { Text = "_Send ↵", X = Pos.Right(_inputField) + 1, Y = 9 };

        _startButton.Accepted += async (_, _) => await StartSelectedAsync();
        _stopButton.Accepted += (_, _) => StopSelected();
        _restartButton.Accepted += async (_, _) => await RestartSelectedAsync();
        _clearButton.Accepted += (_, _) => ClearSelectedLog();
        _sendButton.Accepted += (_, _) => SendInput();
        _inputField.Accepted += (_, _) => SendInput();

        _outputView = new TextView
        {
            X = 1,
            Y = 11,
            Width = Dim.Fill() - 2,
            Height = Dim.Fill() - 1,
            ReadOnly = true,
            WordWrap = false,
            Text = "Choose an application on the left."
        };

        _outputView.SetScheme(CreateLogScheme());
        _invoiceTerminal = new PtyTerminalSession();
        _invoiceTerminal.StateChanged += () => _application.Invoke(RefreshSelectedApp);
        _terminalView = new TerminalView(_application, _invoiceTerminal)
        {
            X = 1,
            Y = 6,
            Width = Dim.Fill() - 2,
            Height = Dim.Fill() - 1,
            Visible = false
        };
        details.Add(_nameLabel, _commandLabel, _statusLabel, _startButton, _stopButton, _restartButton,
            _invoiceMenu, _clearButton, _inputLabel, _inputField, _sendButton, _outputView, _terminalView);
        _partsOrderView = new PartsOrderView(application)
        {
            X = 1, Y = 4, Width = Dim.Fill() - 2, Height = Dim.Fill() - 1, Visible = false
        };
        details.Add(_partsOrderView);
        var retrievalDefinition = definitions.First(d => d.Name == "AutoRetrieval");
        _autoRetrievalView = new AutoRetrievalView(application,
            Path.Combine(retrievalDefinition.WorkingDirectory, ".env"))
        {
            X = 1, Y = 4, Width = Dim.Fill() - 2, Height = Dim.Fill() - 1, Visible = false
        };
        details.Add(_autoRetrievalView);
        _collisionDownloadView = new CollisionDownloadView(application,
            Path.Combine(definitions.First(d => d.Name == "CollisionLink Downloader").WorkingDirectory, ".env"))
        {
            X = 1, Y = 4, Width = Dim.Fill() - 2, Height = Dim.Fill() - 1, Visible = false
        };
        details.Add(_collisionDownloadView);
        _invoiceCreationView = new InvoiceCreationView(application)
        {
            X = 1, Y = 4, Width = Dim.Fill() - 2, Height = Dim.Fill() - 1, Visible = false
        };
        details.Add(_invoiceCreationView);
        _quoteCreationView = new InvoiceCreationView(application, "Quote")
        {
            X = 1, Y = 4, Width = Dim.Fill() - 2, Height = Dim.Fill() - 1, Visible = false
        };
        details.Add(_quoteCreationView);
        _quoteManagementView = new QuoteManagementView(application)
        {
            X = 1, Y = 4, Width = Dim.Fill() - 2, Height = Dim.Fill() - 1, Visible = false
        };
        details.Add(_quoteManagementView);
        _documentSearchView = new DocumentSearchView(application)
        {
            X = 1, Y = 4, Width = Dim.Fill() - 2, Height = Dim.Fill() - 1, Visible = false
        };
        details.Add(_documentSearchView);
        _optionsView = new InvoiceOptionsView(application)
        {
            X = 1, Y = 4, Width = Dim.Fill() - 2, Height = Dim.Fill() - 1, Visible = false
        };
        details.Add(_optionsView);
        _optionsButton = new Button { Text = "Options", X = 1, Y = Pos.AnchorEnd(2), Width = Dim.Fill() - 2, Height = 2 };
        _optionsButton.SetScheme(CreateNavigationButtonScheme());
        _optionsButton.Accepted += async (_, _) =>
        {
            foreach (var child in _workspace.SubViews) child.Visible = false;
            _nameLabel.Visible = _commandLabel.Visible = _statusLabel.Visible = _optionsView.Visible = true;
            _nameLabel.Text = "Options";
            _commandLabel.Text = "Invoice counters and service labor rate";
            _statusLabel.Text = "Separate from invoice creation; changes require Save.";
            _optionsView.SetFocus();
            await _optionsView.ReloadAsync();
        };
        sidebar.Add(_optionsButton);
        _application.Mouse.MouseEvent += OnDashboardMouseEvent;
        _application.Keyboard.KeyDown += OnDashboardKeyDown;
        Add(sidebar, details);
        ApplyResponsiveLayout();
        if (_apps.Count > 0) SelectApp(_apps[0]);
    }

    private void SelectApp(ManagedConsoleApp app)
    {
        _optionsView.Visible = false;
        _selected = app;
        RefreshSelectedApp();
        if (app.Definition.Name == "Parts Order") { _partsOrderView.SetFocus(); return; }
        if (app.Definition.Name == "AutoRetrieval") { _autoRetrievalView.SetFocus(); return; }
        if (app.Definition.Name == "CollisionLink Downloader") { _collisionDownloadView.FocusInput(); return; }
        if (app.Definition.Name == "Invoice Creation") { _invoiceCreationView.FocusInput(); return; }
        if (app.Definition.Name == "Quote Creation") { _quoteCreationView.FocusInput(); return; }
        if (app.Definition.Name == "Manage Quotes") { _ = _quoteManagementView.ReloadAsync(); _quoteManagementView.FocusList(); return; }
        if (app.Definition.Name == "Search Documents") { _documentSearchView.FocusInput(); return; }
        if (IsInvoiceSelected && _invoiceTerminal.IsRunning) _terminalView.SetFocus();
        else if (app.IsRunning) _inputField.SetFocus();
    }

    private void OnDashboardMouseEvent(object? sender, Mouse mouse)
    {
        if (!_optionsView.Visible && _selected?.Definition.Name is not ("Parts Order" or "AutoRetrieval" or "CollisionLink Downloader" or "Invoice Creation" or "Quote Creation" or "Manage Quotes" or "Search Documents")) return;
        // Stop passive pointer motion before it reaches TextField's mouse
        // handling. Clicks and deliberate drag selection still pass through.
        const MouseFlags buttons = MouseFlags.LeftButtonPressed | MouseFlags.MiddleButtonPressed |
            MouseFlags.RightButtonPressed | MouseFlags.LeftButtonReleased |
            MouseFlags.MiddleButtonReleased | MouseFlags.RightButtonReleased |
            MouseFlags.LeftButtonClicked | MouseFlags.MiddleButtonClicked | MouseFlags.RightButtonClicked;
        if (mouse.Flags.HasFlag(MouseFlags.PositionReport) && (mouse.Flags & buttons) == 0)
            mouse.Handled = true;
    }

    private void OnDashboardKeyDown(object? sender, Key key)
    {
        if (!_optionsView.Visible && _selected?.Definition.Name is "Invoice Creation" or "Quote Creation" && key == Key.Esc)
        {
            key.Handled = true;
            if (_selected.Definition.Name == "Quote Creation") _quoteCreationView.GoBack();
            else _invoiceCreationView.GoBack();
        }
    }

    private void RefreshSelectedApp()
    {
        ApplyResponsiveLayout();
        if (_optionsView.Visible) return;
        if (_selected is null) return;
        ConsoleAppDefinition definition = _selected.Definition;
        _nameLabel.Text = $"✨  {definition.Name}";
        _commandLabel.Text = $"   {definition.FileName} {definition.Arguments}";
        bool running = SelectedIsRunning;
        int? processId = IsInvoiceSelected ? _invoiceTerminal.ProcessId : _selected.ProcessId;
        _statusLabel.Text = running
            ? $"🟢  RUNNING   PID {processId}"
            : definition.Enabled ? "⚪  READY" : "🧩  RESERVED FOR FUTURE TOOL";
        _startButton.Enabled = definition.Enabled && !running;
        _stopButton.Enabled = running;
        _restartButton.Enabled = definition.Enabled;
        bool browserMode = definition.Name == "AutoRetrieval";
        _startButton.Text = browserMode ? "▶ _Open Browser" : "▶ _Start";
        _stopButton.Visible = true;
        _restartButton.Visible = true;
        bool terminalMode = IsInvoiceSelected;
        // Keep the PTY visible after a failed startup so its real error output
        // remains readable instead of making the terminal appear to vanish.
        bool terminalActive = terminalMode &&
            (running || _invoiceTerminal.HasOutput || _invoiceTerminal.HasStarted);
        _terminalView.Visible = terminalActive;
        _invoiceMenu.Visible = false;
        _clearButton.Visible = !terminalMode;
        _inputLabel.Visible = !terminalMode;
        _inputField.Visible = !terminalMode;
        _sendButton.Visible = !terminalMode;
        _outputView.Visible = !terminalMode;
        bool partsMode = definition.Name == "Parts Order";
        bool creationMode = definition.Name == "Invoice Creation";
        bool quoteCreationMode = definition.Name == "Quote Creation";
        bool quoteManagementMode = definition.Name == "Manage Quotes";
        bool searchMode = definition.Name == "Search Documents";
        _documentSearchView.Visible = searchMode;
        _invoiceCreationView.Visible = creationMode;
        _quoteCreationView.Visible = quoteCreationMode;
        _quoteManagementView.Visible = quoteManagementMode;
        bool collisionMode = definition.Name == "CollisionLink Downloader";
        _collisionDownloadView.Visible = collisionMode;
        _partsOrderView.Visible = partsMode;
        _autoRetrievalView.Visible = browserMode;
        _startButton.Visible = !partsMode && !browserMode && !collisionMode && !creationMode && !quoteCreationMode && !quoteManagementMode && !searchMode;
        if (partsMode || browserMode || collisionMode || creationMode || quoteCreationMode || quoteManagementMode || searchMode)
        {
            _commandLabel.Text = partsMode ? "Integrated Markdown PO service" : "Integrated AutoRetrieval services";
            _statusLabel.Text = partsMode ? "Fill in the order and click Save PO." : "Enter claim and registration, then start retrieval.";
            if (collisionMode)
            {
                _commandLabel.Text = "Integrated CollisionLink download services";
                _statusLabel.Text = "Enter a claim number, then click Download.";
            }
            if (creationMode)
            {
                _commandLabel.Text = "Guided invoice creation";
                _statusLabel.Text = "Enter: next • Esc: back • Clicks and arrow choices supported";
            }
            if (quoteCreationMode)
            {
                _commandLabel.Text = "Guided quote creation";
                _statusLabel.Text = "Enter: next • Esc: back • Clicks supported";
            }
            if (quoteManagementMode)
            {
                _commandLabel.Text = "Manage generated quotes";
                _statusLabel.Text = "Select a quote, then convert to invoice or remove it.";
            }
            if (searchMode)
            {
                _commandLabel.Text = "Search number, customer, vehicle, VIN, stock, PO, claim or type";
                _statusLabel.Text = "Click / arrows: select • Enter: details • Esc: return to results";
            }
            _stopButton.Visible = _restartButton.Visible = _clearButton.Visible = false;
            _inputLabel.Visible = _inputField.Visible = _sendButton.Visible = _outputView.Visible = false;
        }
        _inputField.Enabled = !terminalMode && running;
        _sendButton.Enabled = !terminalMode && running;
        if (!terminalMode)
        {
            _outputView.Text = _logs[_selected].Length == 0
                ? browserMode
                    ? "Enter a claim number (AB12345-6 or AB123456) and then an 8-digit registration number after starting.\nAutoRetrieval then opens Chromium and signs in.\nThe browser stays open for inspection; close its window when finished."
                    : "No output yet."
                : _logs[_selected].ToString();
            _outputView.MoveEnd();
        }
        SetNeedsDraw();
    }

    protected override void OnViewportChanged(DrawEventArgs e)
    {
        base.OnViewportChanged(e);
        ApplyResponsiveLayout();
    }

    private void ApplyResponsiveLayout()
    {
        int totalWidth = Math.Max(0, Viewport.Width);
        int sidebarWidth = totalWidth switch
        {
            >= 120 => 30,
            >= 96 => 26,
            >= 76 => 22,
            _ => 18
        };
        _sidebar.Width = sidebarWidth;
        _sidebarHeader.Text = sidebarWidth <= 18 ? "TOOLS" : sidebarWidth <= 22 ? "TOOLS" : "🔌 CONNECTED TOOLS";

        foreach ((ManagedConsoleApp app, Button button) in _navigationButtons)
            button.Text = GetNavigationLabel(app.Definition, sidebarWidth <= 22);

        int workspaceWidth = Math.Max(0, totalWidth - sidebarWidth);
        bool narrowWorkspace = workspaceWidth < 56;

        _nameLabel.X = narrowWorkspace ? 1 : 2;
        _commandLabel.X = narrowWorkspace ? 1 : 2;
        _statusLabel.X = narrowWorkspace ? 1 : 2;
        _nameLabel.Width = Dim.Fill() - (narrowWorkspace ? 2 : 3);
        _commandLabel.Width = Dim.Fill() - (narrowWorkspace ? 2 : 3);
        _statusLabel.Width = Dim.Fill() - (narrowWorkspace ? 2 : 3);

        _startButton.X = 1;
        _startButton.Y = 4;
        _stopButton.X = Pos.Right(_startButton) + 1;
        _stopButton.Y = 4;
        _restartButton.X = narrowWorkspace ? 1 : Pos.Right(_stopButton) + 1;
        _restartButton.Y = narrowWorkspace ? 6 : 4;

        _invoiceMenu.X = 1;
        _invoiceMenu.Y = narrowWorkspace ? 8 : 6;
        _invoiceMenu.Width = narrowWorkspace ? Dim.Fill() - 2 : 25;
        _clearButton.X = narrowWorkspace ? 1 : 28;
        _clearButton.Y = narrowWorkspace ? 10 : 6;

        _inputLabel.X = 1;
        _inputLabel.Y = narrowWorkspace ? 12 : 8;
        _inputLabel.Width = Dim.Fill() - 2;
        _inputField.X = 1;
        _inputField.Y = narrowWorkspace ? 13 : 9;
        _inputField.Width = narrowWorkspace ? Dim.Fill() - 2 : Dim.Fill() - 13;
        _sendButton.X = narrowWorkspace ? 1 : Pos.Right(_inputField) + 1;
        _sendButton.Y = narrowWorkspace ? 14 : 9;

        int outputTop = narrowWorkspace ? 16 : 11;
        _outputView.Y = outputTop;
        _outputView.Height = Dim.Fill() - 1;
        _terminalView.Y = narrowWorkspace ? 8 : 6;
        _terminalView.Height = Dim.Fill() - 1;
    }

    private bool IsInvoiceSelected => _selected?.Definition.Name == "Invoice Generator";
    private bool SelectedIsRunning => IsInvoiceSelected ? _invoiceTerminal.IsRunning : _selected?.IsRunning == true;

    private async Task StartSelectedAsync()
    {
        if (_selected is null) return;
        if (_selected.Definition.Name == "Parts Order") { _partsOrderView.SetFocus(); return; }
        if (_selected.Definition.Name == "AutoRetrieval") { _autoRetrievalView.SetFocus(); return; }
        if (_selected.Definition.Name == "CollisionLink Downloader") { _collisionDownloadView.FocusInput(); return; }
        if (_selected.Definition.Name == "Invoice Creation") { _invoiceCreationView.FocusInput(); return; }
        if (_selected.Definition.Name == "Quote Creation") { _quoteCreationView.FocusInput(); return; }
        if (_selected.Definition.Name == "Manage Quotes") { _quoteManagementView.FocusList(); return; }
        if (_selected.Definition.Name == "Search Documents") { _documentSearchView.FocusInput(); return; }
        try
        {
            if (IsInvoiceSelected)
            {
                await _invoiceTerminal.StartAsync(_selected.Definition,
                    Math.Max(2, _terminalView.Viewport.Width), Math.Max(2, _terminalView.Viewport.Height));
                _terminalView.SetFocus();
            }
            else _selected.Start();
        }
        catch (Exception exception) { AppendLog(_selected, $"[{DateTime.Now:HH:mm:ss}] ERROR: {exception.Message}"); }
        RefreshSelectedApp();
    }

    private void StopSelected()
    {
        if (_selected is null) return;
        try
        {
            if (IsInvoiceSelected) _invoiceTerminal.Stop();
            else _selected.Stop();
        }
        catch (Exception exception) { AppendLog(_selected, $"[{DateTime.Now:HH:mm:ss}] ERROR: {exception.Message}"); }
        RefreshSelectedApp();
    }

    private async Task RestartSelectedAsync()
    {
        StopSelected();
        await StartSelectedAsync();
    }

    private void RunSelected(Action<ManagedConsoleApp> action)
    {
        if (_selected is null) return;
        try { action(_selected); }
        catch (Exception exception) { AppendLog(_selected, $"[{DateTime.Now:HH:mm:ss}] ERROR: {exception.Message}"); }
        RefreshSelectedApp();
        if (_selected.IsRunning) _inputField.SetFocus();
    }

    private void OnOutput(ManagedConsoleApp app, string line) => _application.Invoke(() =>
    {
        const string clearMarker = "\u001eASC_CLEAR\u001e";
        int clearIndex = line.LastIndexOf(clearMarker, StringComparison.Ordinal);
        if (clearIndex >= 0)
        {
            _logs[app].Clear();
            line = line[(clearIndex + clearMarker.Length)..];
        }

        AppendLog(app, line);
        if (_selected == app) RefreshSelectedApp();
    });

    private void AppendLog(ManagedConsoleApp app, string text)
    {
        StringBuilder log = _logs[app];
        log.Append(text);
        if (log.Length > MaxLogCharacters) log.Remove(0, log.Length - MaxLogCharacters);
    }

    private void ClearSelectedLog()
    {
        if (_selected is null) return;
        _logs[_selected].Clear();
        RefreshSelectedApp();
    }

    private void SendInput()
    {
        if (_selected is null) return;

        string input = _inputField.Text;
        try
        {
            _selected.SendInput(input);
            _inputField.Text = string.Empty;
            _inputField.SetFocus();
        }
        catch (Exception exception)
        {
            AppendLog(_selected, $"[{DateTime.Now:HH:mm:ss}] ERROR: {exception.Message}");
        }
        RefreshSelectedApp();
    }

    private void SendQuickInput(string value)
    {
        if (_selected is null || !_selected.IsRunning ||
            _selected.Definition.Name != "Invoice Generator") return;

        _ = _invoiceTerminal.SendAsync(value + "\r");
        _terminalView.SetFocus();
    }

    private MenuBar CreateInvoiceMenu()
    {
        (string Label, string Value)[] actions =
        [
            ("🧾 Create invoice", "1"),
            ("📝 Create quote", "2"),
            ("🔎 Search documents", "3"),
            ("📚 Manage quotes", "4"),
            ("🔢 View counters", "5"),
            ("⚙ Configure counters", "6"),
            ("💵 Configure labor rate", "7"),
            ("🚪 Exit", "8")
        ];

        List<View> menuItems = [];
        foreach ((string label, string value) in actions)
        {
            MenuItem item = new() { Title = label };
            item.Accepted += (_, _) => SendQuickInput(value);
            menuItems.Add(item);
        }

        MenuBarItem invoiceActions = new("🖱 _Invoice actions", menuItems);
        return new MenuBar([invoiceActions]);
    }

    private static Scheme CreateDashboardScheme() => new(
        new Terminal.Gui.Drawing.Attribute(
            ColorName16.White,
            ColorName16.DarkGray));

    private static Scheme CreateSidebarScheme() => new(
        new Terminal.Gui.Drawing.Attribute(
            ColorName16.BrightCyan,
            ColorName16.DarkGray));

    private static Scheme CreateNavigationButtonScheme() =>
        ButtonVisuals.CreateScheme(ColorName16.White, ColorName16.Gray, ColorName16.BrightCyan);

    private static Scheme CreatePanelScheme() => new(
        new Terminal.Gui.Drawing.Attribute(
            ColorName16.White,
            ColorName16.DarkGray));

    private static Scheme CreateLogScheme() => new(
        new Terminal.Gui.Drawing.Attribute(
            ColorName16.BrightGreen,
            ColorName16.DarkGray));

    private static Scheme CreateActionScheme(ColorName16 accent) =>
        ButtonVisuals.CreateScheme(accent, ColorName16.DarkGray, accent);

    private static string GetNavigationLabel(ConsoleAppDefinition definition, bool compact = false)
    {
        if (compact)
        {
            return definition.Name switch
            {
                "CollisionLink Downloader" => "Collision",
                "Invoice Generator" => "Generator",
                "Invoice Creation" => "Create",
                "Quote Creation" => "Quote",
                "Manage Quotes" => "Quotes",
                "Search Documents" => "Search",
                "AutoRetrieval" => "Retrieval",
                "Parts Order" => "Parts",
                _ when !definition.Enabled => "Future",
                _ => definition.Name
            };
        }

        return definition.Name switch
    {
        "CollisionLink Downloader" => "📥 Collision Download",
        "Invoice Generator" => "🧾 Invoice Generator",
        "Invoice Creation" => "📝 Invoice Creation",
        "Quote Creation" => "Quote Creation",
        "Manage Quotes" => "Manage Quotes",
        "Search Documents" => "Search Documents",
        "AutoRetrieval" => "🌐 AutoRetrieval",
        "Parts Order" => "📦 Parts Order",
        _ when !definition.Enabled => "🧩 Future Tool",
        _ => definition.Name
    };
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _application.Mouse.MouseEvent -= OnDashboardMouseEvent;
            _application.Keyboard.KeyDown -= OnDashboardKeyDown;
            _invoiceTerminal.Dispose();
            foreach (ManagedConsoleApp app in _apps) app.Dispose();
        }
        base.Dispose(disposing);
    }
}
#pragma warning restore CS0618
