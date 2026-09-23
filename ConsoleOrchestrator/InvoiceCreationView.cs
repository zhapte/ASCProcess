using InvoiceGenerator.Models;
using InvoiceGenerator.Services;
using Terminal.Gui.App;
using Terminal.Gui.Drawing;
using Terminal.Gui.Input;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

namespace ConsoleOrchestrator;

#pragma warning disable CS0618
public sealed class InvoiceCreationView : View
{
    private sealed class CompactButton : Button
    {
        protected override void OnInitializingShadowStyle(ValueChangingEventArgs<ShadowStyles?> args)
        {
            args.NewValue = null;
        }
    }
    private readonly IApplication _application;
    private readonly string _documentType;
    private readonly bool _chooseVariant;
    private InvoiceDraftWorkflow _draft = new();
    private readonly Dictionary<string, string> _pending = new();
    private readonly Label _prompt = new() { X = 0, Y = 0, Width = Dim.Fill(), Height = 1 };
    private readonly TextField _input = new() { X = 0, Y = 1, Width = Dim.Fill() - 1, MouseHighlightStates = MouseState.None };
    private readonly Button[] _choices;
    private readonly Button _back = new CompactButton() { Text = "Back", X = 0, Y = 5 };
    private readonly Button _next = new CompactButton() { Text = "Next", X = 11, Y = 5 };
    private readonly Button _generate = new CompactButton() { Text = "Generate", X = 11, Y = 5 };
    private readonly Button _reset = new CompactButton() { Text = "New invoice", X = 25, Y = 5 };
    private readonly Label _message = new() { X = 0, Y = 6, Width = Dim.Fill(), Height = 2 };
    private readonly TextView _summary = new() { X = 0, Y = 8, Width = Dim.Fill(), Height = Dim.Fill(), ReadOnly = true, WordWrap = true, MouseHighlightStates = MouseState.None };
    private int _step;
    private int _choice;
    private string[]? _options;
    private bool _busy;
    private bool _generated;
    private bool _disposed;
    private DocumentRecord? _review;

    public InvoiceCreationView(IApplication application, string documentType = "Invoice")
    {
        _application = application;
        _documentType = documentType;
        _chooseVariant = string.Equals(documentType, "Invoice", StringComparison.OrdinalIgnoreCase);
        _reset.Text = _chooseVariant ? "New invoice" : "New quote";
        CanFocus = true;
        var normal = new Terminal.Gui.Drawing.Attribute(ColorName16.White, ColorName16.DarkGray);
        _summary.SetScheme(new Scheme(normal) { ReadOnly = normal, Focus = normal, Editable = normal, Active = normal });
        // Compact, single-line choices leave the draft output visible in a window.
        _choices = Enumerable.Range(0, 3).Select(i => new CompactButton { Y = 2, Height = 1, BorderStyle = LineStyle.None, MouseHighlightStates = MouseState.None }).ToArray();
        _choices[0].X = 0;
        for (int i = 1; i < _choices.Length; i++) _choices[i].X = Pos.Right(_choices[i - 1]) + 1;
        foreach (var button in _choices.Concat(new[] { _back, _next, _generate, _reset }))
        {
            button.BorderStyle = LineStyle.None;
            button.Height = 1;
        }
        _message.Y = Pos.Bottom(_back) + 1;
        _summary.Y = Pos.Bottom(_message);
        Add(_prompt, _input, _back, _next, _generate, _reset, _message, _summary);
        for (int i = 0; i < _choices.Length; i++)
        {
            int index = i;
            Add(_choices[i]);
            _choices[i].Accepted += (_, _) => { _choice = index; Advance(); };
            _choices[i].KeyDown += (_, key) =>
            {
                if (key == Key.CursorUp || key == Key.CursorDown || key == Key.CursorLeft || key == Key.CursorRight)
                {
                    _choice = (index + (key == Key.CursorUp || key == Key.CursorLeft ? 2 : 1)) % 3;
                    _choices[_choice].SetFocus();
                    key.Handled = true;
                }
                else if (key == Key.Esc) { GoBack(); key.Handled = true; }
            };
        }
        _input.Accepted += (_, _) => Advance();
        _input.KeyDown += (_, key) => { if (key == Key.Esc) { GoBack(); key.Handled = true; } };
        _back.Accepted += (_, _) => GoBack();
        _next.Accepted += (_, _) => Advance();
        _generate.Accepted += async (_, _) => await GenerateAsync();
        _reset.Accepted += (_, _) => { if (_busy) return; _draft = new(); _pending.Clear(); _step = 0; _generated = false; _message.Text = ""; Render(); };
        ButtonVisuals.Apply(this);
        Render();
    }

    public void FocusInput()
    {
        if (_busy) return;
        if (_options is not null) _choices[_choice].SetFocus();
        else if (_input.Visible) _input.SetFocus();
        else _back.SetFocus();
    }

    private bool Store()
    {
        if (_chooseVariant && _step == 0) { _draft.Variant = _options![_choice]; return true; }
        var prompts = _draft.Prompts();
        int promptIndex = _chooseVariant ? _step - 1 : _step;
        if (promptIndex >= prompts.Count) return true;
        string? error = _draft.Set(prompts[promptIndex], _options is null ? _input.Text : _options[_choice]);
        if (error is null) _pending.Remove(prompts[promptIndex].Key);
        _message.Text = error ?? "";
        return error is null;
    }

    private void Advance()
    {
        if (_busy || _generated || !Store()) return;
        int lastEntryStep = _chooseVariant ? _draft.Prompts().Count : _draft.Prompts().Count - 1;
        if (_step <= lastEntryStep) _step++;
        Render();
    }

    public void GoBack()
    {
        if (_busy || _generated || _step == 0) return;
        var prompts = _draft.Prompts();
        int promptIndex = _chooseVariant ? _step - 1 : _step;
        if (promptIndex >= 0 && promptIndex < prompts.Count && _options is null)
            _pending[prompts[promptIndex].Key] = _input.Text;
        // Going back is always allowed, even from an incomplete field.
        Store();
        _step--;
        Render();
    }

    private void Render()
    {
        var prompts = _draft.Prompts();
        int reviewStep = _chooseVariant ? prompts.Count + 1 : prompts.Count;
        _step = Math.Min(_step, reviewStep);
        bool review = _step >= reviewStep;
        _review = null;
        int promptIndex = _chooseVariant ? _step - 1 : _step;
        _options = _chooseVariant && _step == 0 ? ["Regular", "Calibration", "Service"] :
            review ? null : prompts[promptIndex].Choices;
        // Both text input and choices fit above a compact action row.
        foreach (var button in new[] { _back, _next, _generate, _reset })
            button.Y = 4;
        string value = _chooseVariant && _step == 0 ? _draft.Variant : review ? "" : _draft.Value(prompts[promptIndex]);
        if (!review && promptIndex >= 0 && _pending.TryGetValue(prompts[promptIndex].Key, out string? pending)) value = pending;
        _prompt.Text = _chooseVariant && _step == 0 ? "Choose invoice type (arrows + Enter, or click)" :
            review ? $"Review {_documentType.ToLowerInvariant()} - click Generate to save" :
            $"{promptIndex + 1}/{prompts.Count}: {prompts[promptIndex].Label}";
        _input.Visible = !review && _options is null;
        _input.Text = value;
        _choice = Math.Max(0, _options is null ? 0 : Array.IndexOf(_options, value));
        for (int i = 0; i < _choices.Length; i++)
        {
            _choices[i].Visible = _options is not null;
            _choices[i].Text = _options?[i] ?? "";
        }
        _back.Enabled = _step > 0 && !_generated;
        _next.Visible = !review;
        _generate.Visible = review;
        _generate.Enabled = false;
        _summary.Text = _chooseVariant ? $"{_draft.Variant} invoice draft\n" : "Quote draft\n";
        _summary.Text += string.Join("\n", prompts.Where(p => _draft.Values.ContainsKey(p.Key)).Select(p => $"{p.Label}: {_draft.Value(p)}"));
        if (review)
        {
            try
            {
                _review = _draft.Build(AppSettingsService.Load().Service.LaborRate);
                _review.DocumentType = _documentType;
                _summary.Text += $"\n\nSubtotal: {_review.Subtotal:C2}\nGST: {_review.Gst:C2}\nPST: {_review.Pst:C2}\nTOTAL: {_review.Total:C2}";
                _generate.Enabled = !_generated;
                _message.Text = "Review all details. Nothing is saved until Generate.";
            }
            catch (Exception ex) { _message.Text = ex.Message; }
        }
        if (Visible) FocusInput();
    }

    private async Task GenerateAsync()
    {
        if (_busy || _generated || _review is null) return;
        DocumentRecord draft = _review;
        _busy = true;
        _back.Enabled = _next.Enabled = _generate.Enabled = _reset.Enabled = false;
        _message.Text = "Saving and generating invoice...";
        try
        {
            var result = await Task.Run(() => new InvoiceCreationService().Generate(draft));
            if (!_disposed) _application.Invoke(() =>
            {
                if (_disposed) return;
                _generated = true;
                _message.Text = $"{_documentType} {result.DocumentNumber} saved. Use {_reset.Text} to start another.";
                _summary.Text += $"\n\nStatus: {result.Status}\nWord: {result.WordFilePath}\nPDF: {result.PdfFilePath ?? "Unavailable — Word document generated instead."}";
            });
        }
        catch (Exception ex)
        {
            if (!_disposed) _application.Invoke(() => { if (!_disposed) _message.Text = $"Generation failed: {ex.Message}"; });
        }
        finally
        {
            if (!_disposed) _application.Invoke(() =>
            {
                if (_disposed) return;
                _busy = false;
                _back.Enabled = _generate.Enabled = !_generated;
                _reset.Enabled = true;
            });
        }
    }

    protected override void Dispose(bool disposing) { if (disposing) _disposed = true; base.Dispose(disposing); }
}
#pragma warning restore CS0618
