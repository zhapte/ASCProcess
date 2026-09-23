using System.Collections.ObjectModel;
using System.Text;
using InvoiceGenerator.Models;
using InvoiceGenerator.Services;
using Terminal.Gui.App;
using Terminal.Gui.Drawing;
using Terminal.Gui.Input;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

namespace ConsoleOrchestrator;

#pragma warning disable CS0618
public sealed class QuoteManagementView : View
{
    private sealed class FlatButton : Button
    {
        protected override void OnInitializingShadowStyle(ValueChangingEventArgs<ShadowStyles?> args) => args.NewValue = null;
    }

    private readonly IApplication _app;
    private readonly QuoteManagementService _service = new();
    private readonly Button _refresh = new FlatButton { Text = "Refresh", Height = 1, MouseHighlightStates = MouseState.None };
    private readonly Button _convert = new FlatButton { Text = "Convert", X = 12, Height = 1, MouseHighlightStates = MouseState.None };
    private readonly Button _remove = new FlatButton { Text = "Remove", X = 25, Height = 1, MouseHighlightStates = MouseState.None };
    private readonly Label _status = new() { Y = 1, Width = Dim.Fill(), Height = 2, Text = "Loading quotes..." };
    private readonly ListView _quotes = new() { Y = 3, Width = Dim.Fill(), Height = 6, MouseHighlightStates = MouseState.None };
    private readonly TextView _details = new() { Y = 10, Width = Dim.Fill(), Height = Dim.Fill(), ReadOnly = true, WordWrap = true, MouseHighlightStates = MouseState.None };
    private IReadOnlyList<DocumentRecord> _documents = [];
    private bool _busy, _disposed, _confirmConvert, _confirmRemove;

    public QuoteManagementView(IApplication app)
    {
        _app = app;
        CanFocus = true;
        var normal = new Terminal.Gui.Drawing.Attribute(ColorName16.White, ColorName16.DarkGray);
        _details.SetScheme(new Scheme(normal) { ReadOnly = normal, Focus = normal, Editable = normal, Active = normal });
        _quotes.SetSource(new ObservableCollection<string>());
        _quotes.ValueChanged += (_, _) => { ClearConfirmations(); ShowSelected(); };
        _quotes.Accepted += (_, _) => { ShowSelected(); _details.SetFocus(); };
        _details.KeyDown += (_, key) => { if (key == Key.Esc) { _quotes.SetFocus(); key.Handled = true; } };
        _refresh.Accepted += async (_, _) => await ReloadAsync();
        _convert.Accepted += async (_, _) => await ConvertSelectedAsync();
        _remove.Accepted += async (_, _) => await RemoveSelectedAsync();
        Add(_refresh, _convert, _remove, _status, _quotes, _details);
        ButtonVisuals.Apply(this);
    }

    public void FocusList()
    {
        if (_documents.Count > 0) _quotes.SetFocus();
        else _refresh.SetFocus();
    }

    public async Task ReloadAsync()
    {
        if (_busy) return;
        _busy = true;
        _refresh.Enabled = _convert.Enabled = _remove.Enabled = false;
        _status.Text = "Loading quotes...";
        _details.Text = "";
        try
        {
            var documents = await Task.Run(_service.ListQuotes);
            if (!_disposed) _app.Invoke(() =>
            {
                if (_disposed) return;
                _documents = documents;
                _quotes.SetSource(new ObservableCollection<string>(_documents.Select(QuoteRow)));
                _status.Text = _documents.Count == 0 ? "No quotes were found." : $"{_documents.Count} quote(s).";
                if (_documents.Count > 0)
                {
                    _quotes.SelectedItem = 0;
                    ShowSelected();
                    if (Visible) _quotes.SetFocus();
                }
            });
        }
        catch (Exception ex)
        {
            if (!_disposed) _app.Invoke(() => { if (!_disposed) { _status.Text = "Could not load quotes."; _details.Text = ex.Message; } });
        }
        finally
        {
            if (!_disposed) _app.Invoke(() =>
            {
                if (_disposed) return;
                _busy = false;
                SetActionState();
            });
        }
    }

    private async Task ConvertSelectedAsync()
    {
        DocumentRecord? quote = SelectedQuote();
        if (_busy || quote is null) return;
        if (!_confirmConvert)
        {
            _confirmConvert = true;
            _confirmRemove = false;
            _status.Text = $"Click Convert again to create an invoice from quote {quote.DocumentNumber}.";
            SetActionState();
            return;
        }

        _busy = true;
        _refresh.Enabled = _convert.Enabled = _remove.Enabled = false;
        _status.Text = $"Converting quote {quote.DocumentNumber}...";
        try
        {
            DocumentRecord invoice = await Task.Run(() => _service.ConvertToInvoice(quote.Id));
            if (!_disposed) _app.Invoke(() =>
            {
                if (_disposed) return;
                ClearConfirmations();
                _status.Text = $"Created invoice {invoice.DocumentNumber} from quote {quote.DocumentNumber}.";
                _details.Text += $"\n\nConverted invoice: {invoice.DocumentNumber}\nStatus: {invoice.Status}\nPDF: {invoice.PdfFilePath ?? "Unavailable"}";
            });
        }
        catch (Exception ex)
        {
            if (!_disposed) _app.Invoke(() => { if (!_disposed) _status.Text = $"Convert failed: {ex.Message}"; });
        }
        finally
        {
            if (!_disposed) _app.Invoke(() =>
            {
                if (_disposed) return;
                _busy = false;
                SetActionState();
            });
        }
    }

    private async Task RemoveSelectedAsync()
    {
        DocumentRecord? quote = SelectedQuote();
        if (_busy || quote is null) return;
        if (!_confirmRemove)
        {
            _confirmRemove = true;
            _confirmConvert = false;
            _status.Text = $"Click Remove again to delete quote {quote.DocumentNumber}.";
            SetActionState();
            return;
        }

        _busy = true;
        _refresh.Enabled = _convert.Enabled = _remove.Enabled = false;
        _status.Text = $"Removing quote {quote.DocumentNumber}...";
        try
        {
            await Task.Run(() => _service.RemoveQuote(quote.Id));
            if (!_disposed) _app.Invoke(() =>
            {
                if (_disposed) return;
                ClearConfirmations();
                _status.Text = $"Removed quote {quote.DocumentNumber}.";
                _documents = _documents.Where(document => document.Id != quote.Id).ToList();
                _quotes.SetSource(new ObservableCollection<string>(_documents.Select(QuoteRow)));
                if (_documents.Count > 0)
                {
                    _quotes.SelectedItem = 0;
                    ShowSelected();
                }
                else
                {
                    _details.Text = "";
                    _status.Text = "Removed quote. No quotes remain.";
                }
            });
        }
        catch (Exception ex)
        {
            if (!_disposed) _app.Invoke(() => { if (!_disposed) _status.Text = $"Remove failed: {ex.Message}"; });
        }
        finally
        {
            if (!_disposed) _app.Invoke(() =>
            {
                if (_disposed) return;
                _busy = false;
                SetActionState();
            });
        }
    }

    private DocumentRecord? SelectedQuote()
    {
        int index = _quotes.SelectedItem ?? -1;
        return index >= 0 && index < _documents.Count ? _documents[index] : null;
    }

    private void ShowSelected()
    {
        DocumentRecord? quote = SelectedQuote();
        if (quote is null) { _details.Text = ""; SetActionState(); return; }
        var text = new StringBuilder()
            .AppendLine($"Quote {quote.DocumentNumber ?? "(draft)"}")
            .AppendLine($"Date: {quote.CreatedDate:yyyy-MM-dd}   Status: {quote.Status}")
            .AppendLine($"Customer: {quote.CustomerName}")
            .AppendLine($"Vehicle: {quote.YearAndMake}\nVIN: {quote.Vin}")
            .AppendLine($"Stock: {quote.StockNumber}\nPO: {quote.PurchaseOrder}\nClaim: {quote.ClaimNumber}");
        foreach (var item in quote.Items)
            text.AppendLine($"\n{item.Quantity} x {item.ItemNumber} {item.Description}\nUnit: {item.UnitPrice:C2}  Discount: {item.Discount:C2}  Line: {item.LineTotal:C2}");
        text.AppendLine($"\nSubtotal: {quote.Subtotal:C2}\nGST: {quote.Gst:C2}\nPST: {quote.Pst:C2}\nTotal: {quote.Total:C2}")
            .AppendLine($"\nWord: {quote.WordFilePath ?? "Not generated"}\nPDF: {quote.PdfFilePath ?? "Not generated"}");
        _details.Text = text.ToString();
        _details.MoveHome();
        SetActionState();
    }

    private void SetActionState()
    {
        bool hasQuote = !_busy && SelectedQuote() is not null;
        _refresh.Enabled = !_busy;
        _convert.Enabled = hasQuote;
        _remove.Enabled = hasQuote;
        _convert.Text = _confirmConvert ? "Confirm Convert" : "Convert";
        _remove.Text = _confirmRemove ? "Confirm Remove" : "Remove";
    }

    private void ClearConfirmations()
    {
        _confirmConvert = false;
        _confirmRemove = false;
    }

    private static string QuoteRow(DocumentRecord quote) =>
        $"{quote.DocumentNumber ?? "(draft)"} | {quote.CustomerName} | {quote.CreatedDate:yyyy-MM-dd} | {quote.Total:C2} | {quote.Status}";

    protected override void Dispose(bool disposing)
    {
        if (disposing) _disposed = true;
        base.Dispose(disposing);
    }
}
#pragma warning restore CS0618
