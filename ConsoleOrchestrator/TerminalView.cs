using System.Text;
using Terminal.Gui.App;
using Terminal.Gui.Drawing;
using Terminal.Gui.Input;
using Terminal.Gui.ViewBase;
using XTerm;
using XTerm.Input;
using GuiKey = Terminal.Gui.Input.Key;
using GuiMouse = Terminal.Gui.Input.Mouse;
using TermKey = XTerm.Input.Key;
using TermModifiers = XTerm.Input.KeyModifiers;

namespace ConsoleOrchestrator;

public sealed class TerminalView : View
{
    private readonly IApplication _application;
    private readonly PtyTerminalSession _session;
    private int _drawScheduled;

    public TerminalView(IApplication application, PtyTerminalSession session)
    {
        _application = application;
        _session = session;
        CanFocus = true;
        _session.ScreenChanged += ScheduleDraw;
    }

    protected override bool OnDrawingContent(DrawContext? context)
    {
        XTerm.Terminal terminal = _session.Terminal;
        SetAttribute(new Terminal.Gui.Drawing.Attribute(ColorName16.White, ColorName16.Black));

        lock (terminal)
        {
            var buffer = terminal.Buffer;
            int rows = Math.Min(Viewport.Height, terminal.Rows);
            int cols = Math.Min(Viewport.Width, terminal.Cols);

            for (int row = 0; row < Viewport.Height; row++)
            {
                for (int col = 0; col < Viewport.Width; col++)
                {
                    Move(col, row);
                    if (row >= rows || col >= cols)
                    {
                        AddRune(' ');
                        continue;
                    }

                    var line = buffer.Lines[buffer.YDisp + row];
                    string content = line is null || string.IsNullOrEmpty(line[col].Content)
                        ? " "
                        : line[col].Content;
                    AddStr(content);
                }
            }

            if (terminal.CursorVisible && buffer.Y >= buffer.YDisp &&
                buffer.Y < buffer.YDisp + rows && buffer.X < cols)
            {
                Move(buffer.X, buffer.Y - buffer.YDisp);
                AddStr(" ");
            }
        }
        return true;
    }

    private void ScheduleDraw()
    {
        if (Interlocked.Exchange(ref _drawScheduled, 1) != 0) return;
        _ = Task.Run(async () =>
        {
            await Task.Delay(100).ConfigureAwait(false);
            _application.Invoke(() =>
            {
                Interlocked.Exchange(ref _drawScheduled, 0);
                lock (_session.Terminal)
                {
                    Text = string.Join("\n", _session.Terminal.GetVisibleLines());
                }
                SetNeedsDraw();
            });
        });
    }

    protected override bool OnKeyDown(GuiKey key)
    {
        if (key == GuiKey.Q.WithCtrl)
        {
            _application.RequestStop();
            return true;
        }

        TermModifiers modifiers = TermModifiers.None;
        if (key.IsShift) modifiers |= TermModifiers.Shift;
        if (key.IsAlt) modifiers |= TermModifiers.Alt;
        if (key.IsCtrl) modifiers |= TermModifiers.Control;

        TermKey? special = key.NoShift.NoAlt.NoCtrl == GuiKey.Enter ? TermKey.Enter
            : key.NoShift.NoAlt.NoCtrl == GuiKey.Tab ? TermKey.Tab
            : key.NoShift.NoAlt.NoCtrl == GuiKey.Backspace ? TermKey.Backspace
            : key.NoShift.NoAlt.NoCtrl == GuiKey.Esc ? TermKey.Escape
            : key.NoShift.NoAlt.NoCtrl == GuiKey.CursorUp ? TermKey.UpArrow
            : key.NoShift.NoAlt.NoCtrl == GuiKey.CursorDown ? TermKey.DownArrow
            : key.NoShift.NoAlt.NoCtrl == GuiKey.CursorLeft ? TermKey.LeftArrow
            : key.NoShift.NoAlt.NoCtrl == GuiKey.CursorRight ? TermKey.RightArrow
            : key.NoShift.NoAlt.NoCtrl == GuiKey.Home ? TermKey.Home
            : key.NoShift.NoAlt.NoCtrl == GuiKey.End ? TermKey.End
            : key.NoShift.NoAlt.NoCtrl == GuiKey.PageUp ? TermKey.PageUp
            : key.NoShift.NoAlt.NoCtrl == GuiKey.PageDown ? TermKey.PageDown
            : key.NoShift.NoAlt.NoCtrl == GuiKey.DeleteChar ? TermKey.Delete
            : key.NoShift.NoAlt.NoCtrl == GuiKey.InsertChar ? TermKey.Insert
            : null;

        string sequence;
        if (special is not null)
        {
            sequence = _session.Terminal.GenerateKeyInput(special.Value, modifiers);
        }
        else
        {
            string? grapheme = key.AsGrapheme;
            if (string.IsNullOrEmpty(grapheme)) return true;
            StringBuilder builder = new();
            foreach (char character in grapheme) builder.Append(_session.Terminal.GenerateCharInput(character, modifiers));
            sequence = builder.ToString();
        }

        _ = _session.SendAsync(sequence);
        return true;
    }

    protected override bool OnMouseEvent(GuiMouse mouse)
    {
        SetFocus();
        MouseButton button;
        MouseEventType type;
        if (mouse.Flags.HasFlag(MouseFlags.LeftButtonPressed)) { button = MouseButton.Left; type = MouseEventType.Down; }
        else if (mouse.Flags.HasFlag(MouseFlags.LeftButtonReleased) || mouse.Flags.HasFlag(MouseFlags.LeftButtonClicked)) { button = MouseButton.Left; type = MouseEventType.Up; }
        else if (mouse.Flags.HasFlag(MouseFlags.MiddleButtonPressed)) { button = MouseButton.Middle; type = MouseEventType.Down; }
        else if (mouse.Flags.HasFlag(MouseFlags.MiddleButtonReleased)) { button = MouseButton.Middle; type = MouseEventType.Up; }
        else if (mouse.Flags.HasFlag(MouseFlags.RightButtonPressed)) { button = MouseButton.Right; type = MouseEventType.Down; }
        else if (mouse.Flags.HasFlag(MouseFlags.RightButtonReleased)) { button = MouseButton.Right; type = MouseEventType.Up; }
        else if (mouse.Flags.HasFlag(MouseFlags.WheeledUp)) { button = MouseButton.WheelUp; type = MouseEventType.WheelUp; }
        else if (mouse.Flags.HasFlag(MouseFlags.WheeledDown)) { button = MouseButton.WheelDown; type = MouseEventType.WheelDown; }
        else return true;

        var position = mouse.Position.GetValueOrDefault();
        string sequence = _session.Terminal.GenerateMouseEvent(button, position.X, position.Y, type, TermModifiers.None);
        _ = _session.SendAsync(sequence);
        return true;
    }

    protected override void OnViewportChanged(DrawEventArgs e)
    {
        base.OnViewportChanged(e);
        if (Viewport.Width > 1 && Viewport.Height > 1) _session.Resize(Viewport.Width, Viewport.Height);
    }

}
