using PartsOrder.Services;
using Terminal.Gui.App;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

namespace ConsoleOrchestrator;

public sealed class PartsOrderView : View
{
    private readonly IApplication _application;
    private readonly TextField _po;
    private readonly TextField _date;
    private readonly TextField[] _fields;
    private readonly List<PartEntry> _parts = [new("", "", "", "1", "", "")];
    private readonly Label _position;
    private readonly Label _status;
    private readonly View _editor;
    private readonly Button _save;
    private readonly Button _reset;
    private int _index;
    private bool _saving;

    public PartsOrderView(IApplication application)
    {
        _application = application;
        CanFocus = true;
        _editor = new View { Width = Dim.Fill(), Height = 11, CanFocus = true };
        Add(_editor);
        _po = AddField("PO number", 0);
        _date = AddField("Order date", 1);
        _date.Text = DateTime.Today.ToShortDateString();
        _fields = new[] { "Part", "Part number", "Price", "Quantity", "Supplier", "Reason" }
            .Select((label, i) => AddField(label, i + 2)).ToArray();
        _position = new Label { X = 1, Y = 8, Width = Dim.Fill() - 2 };
        _status = new Label { X = 0, Y = 12, Width = Dim.Fill(), Height = 3,
            Text = "Blank part fields use N/A; blank quantity uses 1. Existing POs are not overwritten." };
        _editor.Add(_position);
        var previous = new Button { Text = "Previous", X = 0, Y = 9 };
        var next = new Button { Text = "Next", X = Pos.Right(previous) + 1, Y = 9 };
        var add = new Button { Text = "Add part", X = Pos.Right(next) + 1, Y = 9 };
        var remove = new Button { Text = "Remove", X = Pos.Right(add) + 1, Y = 9 };
        previous.Accepted += (_, _) => Navigate(-1);
        next.Accepted += (_, _) => Navigate(1);
        add.Accepted += (_, _) => { StorePart(); _parts.Add(new("", "", "", "1", "", "")); _index = _parts.Count - 1; LoadPart(); };
        remove.Accepted += (_, _) =>
        {
            if (_parts.Count == 1) { _status.Text = "At least one part is required."; return; }
            _parts.RemoveAt(_index); _index = Math.Min(_index, _parts.Count - 1); LoadPart();
        };
        _editor.Add(previous, next, add, remove);
        _save = new Button { Text = "Save PO", X = 0, Y = 11 };
        _save.Accepted += async (_, _) => await SaveAsync();
        _reset = new Button { Text = "Reset", X = Pos.Right(_save) + 1, Y = 11 };
        _reset.Accepted += (_, _) =>
        {
            if (_saving) return;
            ResetForm();
            _status.Text = string.Empty;
        };
        Add(_save, _reset, _status);
        LoadPart();
        ButtonVisuals.Apply(this);
    }

    private void ResetForm()
    {
        _po.Text = string.Empty;
        _date.Text = string.Empty;
        _parts.Clear();
        _parts.Add(new("", "", "", "", "", ""));
        _index = 0;
        LoadPart();
        if (Visible) _po.SetFocus();
    }

    private TextField AddField(string label, int row)
    {
        var field = new TextField { X = 14, Y = row, Width = Dim.Fill() - 1 };
        _editor.Add(new Label { Text = label, X = 0, Y = row, Width = 13 }, field);
        return field;
    }

    private void StorePart() => _parts[_index] = new(
        Value(0), Value(1), Value(2), Value(3, "1"), Value(4), Value(5));

    private string Value(int index, string fallback = "N/A") =>
        string.IsNullOrWhiteSpace(_fields[index].Text) ? fallback : _fields[index].Text.Trim();

    private void Navigate(int offset)
    {
        StorePart();
        _index = Math.Clamp(_index + offset, 0, _parts.Count - 1);
        LoadPart();
    }

    private void LoadPart()
    {
        var part = _parts[_index];
        string[] values = [part.Part, part.PartNumber, part.Price, part.Quantity, part.Supplier, part.Reason];
        for (int i = 0; i < values.Length; i++) _fields[i].Text = values[i];
        _position.Text = $"Part {_index + 1} of {_parts.Count}";
    }

    private async Task SaveAsync()
    {
        if (_saving) return;
        string po = _po.Text.Trim();
        if (string.IsNullOrWhiteSpace(po)) { _status.Text = "A PO number is required."; _po.SetFocus(); return; }
        string date = string.IsNullOrWhiteSpace(_date.Text) ? DateTime.Today.ToShortDateString() : _date.Text.Trim();
        StorePart();
        PartEntry[] parts = _parts.ToArray();
        _saving = true;
        _editor.Enabled = _save.Enabled = _reset.Enabled = false;
        _status.Text = "Saving PO...";
        string result;
        bool saved = false;
        try
        {
            string path = await Task.Run(async () =>
            {
                string parent = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                    "NAS", "Parts Ordered by Jason");
                if (!Directory.Exists(parent)) throw new IOException("Parts folder unavailable. Check the NAS connection.");
                var service = new MarkdownPoService(Path.Combine(parent, "Order Mark Down"));
                return await service.CreateAsync(po, date, parts, overwrite: false);
            });
            result = $"Saved: {path}";
            saved = true;
        }
        catch (Exception exception) { result = $"Could not save: {exception.Message}"; }
        _application.Invoke(() =>
        {
            _saving = false;
            _editor.Enabled = _save.Enabled = _reset.Enabled = true;
            if (saved) ResetForm();
            _status.Text = result;
        });
    }
}
