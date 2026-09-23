using System.Globalization;
using InvoiceGenerator.Services;
using Terminal.Gui.App;
using Terminal.Gui.Drawing;
using Terminal.Gui.Input;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

namespace ConsoleOrchestrator;

public sealed class InvoiceOptionsView : View
{
    private sealed class FlatButton : Button
    {
        protected override void OnInitializingShadowStyle(ValueChangingEventArgs<ShadowStyles?> args) => args.NewValue = null;
    }
    private readonly IApplication _app;
    private readonly InvoiceOptionsService _service = new();
    private readonly Label _status = new() { Y = 12, Width = Dim.Fill(), Height = 3 };
    private readonly Label _current = new() { Y = 1, Width = Dim.Fill(), Height = 2 };
    private readonly TextField _invoice = new() { X = 15, Y = 4, Width = 10 };
    private readonly TextField _quote = new() { X = 15, Y = 6, Width = 10 };
    private readonly TextField _rate = new() { X = 15, Y = 8, Width = 10 };
    private Dictionary<string, int> _counters = new();
    private string? _confirmation;
    private bool _busy, _disposed;

    public InvoiceOptionsView(IApplication app)
    {
        _app = app;
        CanFocus = true;
        Add(new Label { Text = "Invoice settings", Width = Dim.Fill() }, _current);
        Add(new Label { Text = "Invoice last #", Y = 4 }, _invoice);
        Add(new Label { Text = "Quote last #", Y = 6 }, _quote);
        Add(new Label { Text = "Labor rate", Y = 8 }, _rate);
        AddButton("Save", 27, 4, () => SaveCounter("Invoice", _invoice));
        AddButton("Save", 27, 6, () => SaveCounter("Quote", _quote));
        AddButton("Save", 27, 8, async () =>
        {
            if (!decimal.TryParse(_rate.Text, NumberStyles.Number, CultureInfo.InvariantCulture, out var rate) || rate <= 0)
            { _status.Text = "Enter a positive labor rate."; return; }
            await Run(() => { _service.SetLaborRate(rate); return "Labor rate saved for integrated invoice creation."; });
        });
        AddButton("Refresh", 0, 10, ReloadAsync);
        Add(_status);
        foreach (var field in new[] { _invoice, _quote, _rate }) field.MouseHighlightStates = MouseState.None;
        ButtonVisuals.Apply(this);
    }

    private void AddButton(string text, int x, int y, Func<Task> action)
    {
        var button = new FlatButton { Text = text, X = x, Y = y, Height = 1, MouseHighlightStates = MouseState.None };
        button.Accepted += async (_, _) => { if (!_busy) await action(); };
        Add(button);
    }

    public async Task ReloadAsync()
    {
        _confirmation = null;
        await Run(() =>
        {
            var rate = AppSettingsService.Load().Service.LaborRate;
            var counters = _service.ReadCounters();
            _app.Invoke(() =>
            {
                if (_disposed) return;
                _counters = counters;
                _invoice.Text = counters.GetValueOrDefault("Invoice").ToString(CultureInfo.InvariantCulture);
                _quote.Text = counters.GetValueOrDefault("Quote").ToString(CultureInfo.InvariantCulture);
                _rate.Text = rate.ToString("0.00", CultureInfo.InvariantCulture);
                _current.Text = string.Join("\n", counters.Select(c => $"{c.Key}: last {c.Value:D6} / next {(long)c.Value + 1:D6}"));
            });
            return "Counters are shared with the original generator. Rate applies to this app.";
        });
    }

    private async Task SaveCounter(string type, TextField field)
    {
        if (!_counters.TryGetValue(type, out int expected)) { _status.Text = "Refresh counters first."; return; }
        if (!int.TryParse(field.Text, out int value) || value < expected || value >= 999999)
        { _status.Text = $"Enter a number from {expected} to 999998. Lowering risks duplicate numbers."; return; }
        string confirmation = $"{type}:{expected}:{value}";
        if (_confirmation != confirmation)
        {
            _confirmation = confirmation;
            _status.Text = $"Set {type} last-used to {value:D6}, next to {value + 1:D6}? Click Save again to confirm.";
            return;
        }
        _confirmation = null;
        await Run(() =>
        {
            _service.SetCounter(type, expected, value);
            _app.Invoke(() => { if (!_disposed) { _counters[type] = value; _current.Text = string.Join("\n", _counters.Select(c => $"{c.Key}: last {c.Value:D6} / next {(long)c.Value + 1:D6}")); } });
            return $"{type} counter saved. Next number: {value + 1:D6}.";
        });
    }

    private async Task Run(Func<string> action)
    {
        if (_busy) return;
        _busy = true;
        _status.Text = "Working...";
        try
        {
            string message = await Task.Run(action);
            _app.Invoke(() => { if (!_disposed) _status.Text = message; });
        }
        catch (Exception ex) { _app.Invoke(() => { if (!_disposed) _status.Text = $"Could not update options: {ex.Message}"; }); }
        finally { _busy = false; }
    }

    protected override void Dispose(bool disposing) { if (disposing) _disposed = true; base.Dispose(disposing); }
}
