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
public sealed class DocumentSearchView : View
{
    private sealed class FlatButton : Button
    {
        protected override void OnInitializingShadowStyle(ValueChangingEventArgs<ShadowStyles?> args) => args.NewValue = null;
    }

    private readonly IApplication _app;
    private readonly TextField _query = new() { Width = Dim.Fill(), MouseHighlightStates = MouseState.None };
    private readonly Button _search = new FlatButton { Text = "Search", Y = 2, Height = 1 };
    private readonly Button _reset = new FlatButton { Text = "Reset", X = 12, Y = 2, Height = 1 };
    private readonly Label _status = new() { Y = 3, Width = Dim.Fill(), Height = 1, Text = "Enter a search term, then press Enter." };
    private readonly ListView _results = new() { Y = 4, Width = Dim.Fill(), Height = 4, MouseHighlightStates = MouseState.None };
    private readonly TextView _details = new() { Y = 9, Width = Dim.Fill(), Height = Dim.Fill(), ReadOnly = true, WordWrap = true, MouseHighlightStates = MouseState.None };
    private IReadOnlyList<DocumentRecord> _documents = [];
    private bool _busy, _disposed;

    public DocumentSearchView(IApplication app)
    {
        _app = app;
        CanFocus = true;
        var normal = new Terminal.Gui.Drawing.Attribute(ColorName16.White, ColorName16.DarkGray);
        _details.SetScheme(new Scheme(normal) { ReadOnly = normal, Focus = normal, Editable = normal, Active = normal });
        _results.SetSource(new ObservableCollection<string>());
        _results.ValueChanged += (_, _) => ShowSelected();
        _results.Accepted += (_, _) => { ShowSelected(); _details.SetFocus(); };
        _details.KeyDown += (_, key) => { if (key == Key.Esc) { _results.SetFocus(); key.Handled = true; } };
        _query.Accepted += async (_, _) => await SearchAsync();
        _search.Accepted += async (_, _) => await SearchAsync();
        _reset.Accepted += (_, _) =>
        {
            if (_busy) return;
            _query.Text = "";
            ClearResults();
            _status.Text = "Enter a search term, then press Enter.";
            FocusInput();
        };
        Add(_query, _search, _reset, _status, _results, _details);
    }

    public void FocusInput() => _query.SetFocus();

    private void ClearResults()
    {
        _documents = [];
        _results.SetSource(new ObservableCollection<string>());
        _details.Text = "";
    }

    private async Task SearchAsync()
    {
        if (_busy) return;
        string term = _query.Text.Trim();
        ClearResults();
        if (term.Length == 0) { _status.Text = "Enter a search term."; return; }
        _busy = true;
        _search.Enabled = _reset.Enabled = _query.Enabled = false;
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
                if (Visible && _documents.Count == 0) FocusInput();
            });
        }
    }

    private void ShowSelected()
    {
        int index = _results.SelectedItem ?? -1;
        if (index < 0 || index >= _documents.Count) { _details.Text = ""; return; }
        var d = _documents[index];
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
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) _disposed = true;
        base.Dispose(disposing);
    }
}
#pragma warning restore CS0618
