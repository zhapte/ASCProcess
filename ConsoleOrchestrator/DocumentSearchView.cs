using System.Collections.ObjectModel;
using System.Diagnostics;
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
public sealed class DocumentSearchView : View
{
    private sealed class FlatButton : Button
    {
        protected override void OnInitializingShadowStyle(ValueChangingEventArgs<ShadowStyles?> args) => args.NewValue = null;
    }

    private readonly IApplication _app;
    private readonly QuoteManagementService _quoteService = new();
    private readonly TextField _query = new() { Width = Dim.Fill(), MouseHighlightStates = MouseState.None };
    private readonly Button _search = new FlatButton { Text = "Search", Y = 2, Height = 1 };
    private readonly Button _reset = new FlatButton { Text = "Reset", X = 12, Y = 2, Height = 1 };
    private readonly Button _openWord = new FlatButton { Text = "Open Word", Y = 3, Height = 1, MouseHighlightStates = MouseState.None };
    private readonly Button _openPdf = new FlatButton { Text = "Open PDF", X = 15, Y = 3, Height = 1, MouseHighlightStates = MouseState.None };
    private readonly Button _convertQuote = new FlatButton { Text = "Convert Quote", X = 29, Y = 3, Height = 1, MouseHighlightStates = MouseState.None };
    private readonly Button _removeQuote = new FlatButton { Text = "Remove Quote", X = 48, Y = 3, Height = 1, MouseHighlightStates = MouseState.None };
    private readonly Label _status = new() { Y = 4, Width = Dim.Fill(), Height = 1, Text = "Enter a search term, then press Enter." };
    private readonly ListView _results = new() { Y = 5, Width = Dim.Fill(), Height = 5, MouseHighlightStates = MouseState.None };
    private readonly TextView _details = new() { Y = 11, Width = Dim.Fill(), Height = Dim.Fill(), ReadOnly = true, WordWrap = true, MouseHighlightStates = MouseState.None };
    private IReadOnlyList<DocumentRecord> _documents = [];
    private bool _busy, _disposed, _confirmConvert, _confirmRemove;

    public DocumentSearchView(IApplication app)
    {
        _app = app;
        CanFocus = true;
        var normal = new Terminal.Gui.Drawing.Attribute(ColorName16.White, ColorName16.DarkGray);
        _details.SetScheme(new Scheme(normal) { ReadOnly = normal, Focus = normal, Editable = normal, Active = normal });
        _results.SetSource(new ObservableCollection<string>());
        _results.ValueChanged += (_, _) => { ClearConfirmations(); ShowSelected(); };
        _results.Accepted += (_, _) => { ShowSelected(); _details.SetFocus(); };
        _details.KeyDown += (_, key) => { if (key == Key.Esc) { _results.SetFocus(); key.Handled = true; } };
        _query.Accepted += async (_, _) => await SearchAsync();
        _search.Accepted += async (_, _) => await SearchAsync();
        _openWord.Accepted += (_, _) => OpenSelectedFile(SelectedDocument()?.WordFilePath, "Word");
        _openPdf.Accepted += (_, _) => OpenSelectedFile(SelectedDocument()?.PdfFilePath, "PDF");
        _convertQuote.Accepted += async (_, _) => await ConvertSelectedQuoteAsync();
        _removeQuote.Accepted += async (_, _) => await RemoveSelectedQuoteAsync();
        _reset.Accepted += (_, _) =>
        {
            if (_busy) return;
            _query.Text = "";
            ClearResults();
            _status.Text = "Enter a search term, then press Enter.";
            FocusInput();
        };
        Add(_query, _search, _reset, _openWord, _openPdf, _convertQuote, _removeQuote, _status, _results, _details);
        ButtonVisuals.Apply(this);
        SetActionState();
    }

    public void FocusInput() => _query.SetFocus();

    private void ClearResults()
    {
        _documents = [];
        _results.SetSource(new ObservableCollection<string>());
        _details.Text = "";
        ClearConfirmations();
        SetActionState();
    }

    private async Task SearchAsync()
    {
        if (_busy) return;
        string term = _query.Text.Trim();
        ClearResults();
        if (term.Length == 0) { _status.Text = "Enter a search term."; return; }
        _busy = true;
        _search.Enabled = _reset.Enabled = _query.Enabled = false;
        SetActionState();
        _status.Text = "Searching...";
        try
        {
            var result = await Task.Run(() => new DocumentSearchService().Search(term));
            if (!_disposed) _app.Invoke(() =>
            {
                if (_disposed) return;
                _documents = result.Documents;
                _results.SetSource(new ObservableCollection<string>(_documents.Select(d =>
                    $"{d.DocumentNumber ?? "(draft)"} | {d.DocumentType} | {d.CustomerName} | {d.CreatedDate:yyyy-MM-dd} | {d.Total:C2}")));
                _status.Text = result.HasMore ? "Newest 100 matches. Refine your search for more." : $"{_documents.Count} matching document(s).";
                if (_documents.Count > 0)
                {
                    _results.SelectedItem = 0;
                    ShowSelected();
                    if (Visible) _results.SetFocus();
                }
            });
        }
        catch (Exception ex)
        {
            if (!_disposed) _app.Invoke(() => { if (!_disposed) { _status.Text = "Search failed. Check database access and retry."; _details.Text = ex.Message; } });
        }
        finally
        {
            if (!_disposed) _app.Invoke(() =>
            {
                if (_disposed) return;
                _busy = false;
                _search.Enabled = _reset.Enabled = _query.Enabled = true;
                SetActionState();
                if (Visible && _documents.Count == 0) FocusInput();
            });
        }
    }

    private void ShowSelected()
    {
        DocumentRecord? d = SelectedDocument();
        if (d is null) { _details.Text = ""; SetActionState(); return; }
        var text = new StringBuilder()
            .AppendLine($"{d.DocumentType} {d.DocumentNumber ?? "(draft)"} — {d.InvoiceVariant}")
            .AppendLine($"Date: {d.CreatedDate:yyyy-MM-dd}   Status: {d.Status}")
            .AppendLine($"Customer: {d.CustomerName}")
            .AppendLine($"Vehicle: {d.YearAndMake}\nVIN: {d.Vin}")
            .AppendLine($"Stock: {d.StockNumber}\nPO: {d.PurchaseOrder}\nClaim: {d.ClaimNumber}");
        if (d.InvoiceVariant == "Calibration") text.AppendLine($"Location: {d.CalibrationLocation}\nCalibration: {d.CalibrationType}");
        foreach (var item in d.Items)
            text.AppendLine($"\n{item.Quantity} × {item.ItemNumber} {item.Description}\nUnit: {item.UnitPrice:C2}  Discount: {item.Discount:C2}  Line: {item.LineTotal:C2}");
        text.AppendLine($"\nSubtotal: {d.Subtotal:C2}\nGST: {d.Gst:C2}\nPST: {d.Pst:C2}\nTotal: {d.Total:C2}")
            .AppendLine($"\nWord: {d.WordFilePath ?? "Not generated"}\nPDF: {d.PdfFilePath ?? "Not generated"}");
        _details.Text = text.ToString();
        _details.MoveHome();
        SetActionState();
    }

    private async Task ConvertSelectedQuoteAsync()
    {
        DocumentRecord? document = SelectedDocument();
        if (_busy || document is null || !IsQuote(document)) return;
        if (!_confirmConvert)
        {
            _confirmConvert = true;
            _confirmRemove = false;
            _status.Text = $"Click Convert Quote again to create an invoice from quote {document.DocumentNumber}.";
            SetActionState();
            return;
        }

        _busy = true;
        SetActionState();
        _status.Text = $"Converting quote {document.DocumentNumber}...";
        try
        {
            DocumentRecord invoice = await Task.Run(() => _quoteService.ConvertToInvoice(document.Id));
            if (!_disposed) _app.Invoke(() =>
            {
                if (_disposed) return;
                ClearConfirmations();
                _status.Text = $"Created invoice {invoice.DocumentNumber} from quote {document.DocumentNumber}.";
                _details.Text += $"\n\nConverted invoice: {invoice.DocumentNumber}\nStatus: {invoice.Status}\nWord: {invoice.WordFilePath ?? "Unavailable"}\nPDF: {invoice.PdfFilePath ?? "Unavailable"}";
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

    private async Task RemoveSelectedQuoteAsync()
    {
        DocumentRecord? document = SelectedDocument();
        if (_busy || document is null || !IsQuote(document)) return;
        if (!_confirmRemove)
        {
            _confirmRemove = true;
            _confirmConvert = false;
            _status.Text = $"Click Remove Quote again to delete quote {document.DocumentNumber}.";
            SetActionState();
            return;
        }

        _busy = true;
        SetActionState();
        _status.Text = $"Removing quote {document.DocumentNumber}...";
        try
        {
            await Task.Run(() => _quoteService.RemoveQuote(document.Id));
            if (!_disposed) _app.Invoke(() =>
            {
                if (_disposed) return;
                ClearConfirmations();
                _documents = _documents.Where(item => item.Id != document.Id).ToList();
                _results.SetSource(new ObservableCollection<string>(_documents.Select(d =>
                    $"{d.DocumentNumber ?? "(draft)"} | {d.DocumentType} | {d.CustomerName} | {d.CreatedDate:yyyy-MM-dd} | {d.Total:C2}")));
                _status.Text = $"Removed quote {document.DocumentNumber}.";
                if (_documents.Count > 0)
                {
                    _results.SelectedItem = 0;
                    ShowSelected();
                }
                else
                {
                    _details.Text = "";
                    _status.Text = "Removed quote. No matching documents remain.";
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

    private void OpenSelectedFile(string? path, string fileType)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            _status.Text = $"{fileType} file is not available.";
            return;
        }

        if (!File.Exists(path))
        {
            _status.Text = $"{fileType} file was not found.";
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
            _status.Text = $"Opening {fileType} file.";
        }
        catch (Exception ex)
        {
            _status.Text = $"Could not open {fileType} file: {ex.Message}";
        }
    }

    private DocumentRecord? SelectedDocument()
    {
        int index = _results.SelectedItem ?? -1;
        return index >= 0 && index < _documents.Count ? _documents[index] : null;
    }

    private void SetActionState()
    {
        DocumentRecord? document = SelectedDocument();
        bool canAct = !_busy && document is not null;
        _openWord.Enabled = canAct && File.Exists(document?.WordFilePath ?? "");
        _openPdf.Enabled = canAct && File.Exists(document?.PdfFilePath ?? "");
        _convertQuote.Enabled = canAct && IsQuote(document);
        _removeQuote.Enabled = canAct && IsQuote(document);
        _convertQuote.Text = _confirmConvert ? "Confirm Convert" : "Convert Quote";
        _removeQuote.Text = _confirmRemove ? "Confirm Remove" : "Remove Quote";
    }

    private void ClearConfirmations()
    {
        _confirmConvert = false;
        _confirmRemove = false;
    }

    private static bool IsQuote(DocumentRecord? document) =>
        string.Equals(document?.DocumentType, "Quote", StringComparison.OrdinalIgnoreCase);

    protected override void Dispose(bool disposing)
    {
        if (disposing) _disposed = true;
        base.Dispose(disposing);
    }
}
#pragma warning restore CS0618
