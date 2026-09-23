using Terminal.Gui.Drawing;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

namespace ConsoleOrchestrator;

internal static class ButtonVisuals
{
    public static void Apply(View view)
    {
        var normal = new Terminal.Gui.Drawing.Attribute(ColorName16.White, ColorName16.DarkGray);
        var hover = new Terminal.Gui.Drawing.Attribute(ColorName16.Black, ColorName16.BrightCyan);
        Apply(view, normal, hover);
    }

    public static Scheme CreateScheme(ColorName16 foreground, ColorName16 background, ColorName16 hoverBackground)
    {
        var normal = new Terminal.Gui.Drawing.Attribute(foreground, background);
        var hover = new Terminal.Gui.Drawing.Attribute(ColorName16.Black, hoverBackground);
        return new Scheme(normal)
        {
            Focus = normal,
            Active = normal,
            Highlight = hover
        };
    }

    private static void Apply(View view, Terminal.Gui.Drawing.Attribute normal, Terminal.Gui.Drawing.Attribute hover)
    {
        if (view is Button button)
        {
            button.MouseHighlightStates = MouseState.In;
            button.SetScheme(new Scheme(normal)
            {
                Focus = normal,
                Active = normal,
                Highlight = hover
            });
        }

        foreach (View child in view.SubViews)
            Apply(child, normal, hover);
    }
}
