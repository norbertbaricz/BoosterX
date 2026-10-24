using BoosterX.App.Controls;

namespace BoosterX.App;

/// <summary>Palette and logical dimensions are centralized in Controls/UiPalette.cs.</summary>
internal static class Theme
{
    internal static readonly Color Background = UiPalette.Background;
    internal static readonly Color Surface = UiPalette.Surface;
    internal static readonly Color Card = UiPalette.Card;
    internal static readonly Color Border = UiPalette.Border;
    internal static readonly Color Text = UiPalette.Text;
    internal static readonly Color Muted = UiPalette.Muted;
    internal static readonly Color Accent = UiPalette.Accent;
    internal static readonly Color AccentHover = UiPalette.AccentHover;
    internal static readonly Color Selection = UiPalette.Selection;
    internal const int LogRefreshMilliseconds = 200;
    internal const int MinimumClientWidth = 900;
    internal const int MinimumClientHeight = 620;

    internal static void InitializeNativeColors()
    {
        if (!OperatingSystem.IsWindowsVersionAtLeast(10, 0, 22000) || SystemInformation.HighContrast) return;
        // Optional enhancement only: a native-theme refusal must not prevent the optimizer from starting.
        try
        {
        // .NET 10 marks this API experimental. Keep its use isolated; owner-painted controls also work on Windows 10.
#pragma warning disable WFO5001
            Application.SetColorMode(SystemColorMode.Dark);
#pragma warning restore WFO5001
        }
        catch (Exception ex) when (ex is InvalidOperationException or NotSupportedException or System.ComponentModel.Win32Exception) { }
    }

    internal static void Apply(Control root)
    {
        foreach (Control control in Descendants(root).Prepend(root))
        {
            control.ForeColor = Text;
            control.BackColor = control.Tag as string == "card" ? Card : control is RoundedPanel ? Surface : Background;
            switch (control)
            {
                case Label label:
                    label.BackColor = Color.Transparent;
                    if (label.Tag as string == "muted") label.ForeColor = Muted;
                    break;
                case Button button: StyleButton(button, button.Tag as string == "accent"); break;
                case CheckBox check:
                    check.UseVisualStyleBackColor = false;
                    check.BackColor = Background;
                    break;
                case DataGridView grid:
                    grid.EnableHeadersVisualStyles = false;
                    grid.BackgroundColor = Surface;
                    grid.GridColor = Border;
                    grid.DefaultCellStyle.BackColor = Surface;
                    grid.DefaultCellStyle.ForeColor = Text;
                    grid.DefaultCellStyle.SelectionBackColor = Selection;
                    grid.DefaultCellStyle.SelectionForeColor = Text;
                    grid.AlternatingRowsDefaultCellStyle.BackColor = Card;
                    grid.ColumnHeadersDefaultCellStyle.BackColor = Card;
                    grid.ColumnHeadersDefaultCellStyle.ForeColor = Text;
                    grid.ColumnHeadersDefaultCellStyle.SelectionBackColor = Card;
                    grid.ColumnHeadersDefaultCellStyle.SelectionForeColor = Text;
                    break;
                case DarkTextView view:
                    view.BackColor = Surface;
                    view.ForeColor = Text;
                    break;
                case DarkSearchBox search:
                    search.BackColor = UiPalette.ButtonSurface;
                    search.ForeColor = Text;
                    break;
                case DarkGridHost host:
                    host.BackColor = Surface;
                    host.Viewport.BackColor = Surface;
                    break;
                case TextBoxBase text:
                    text.BackColor = text.Parent is DarkSearchBox ? UiPalette.ButtonSurface : Surface;
                    text.ForeColor = Text;
                    text.BorderStyle = BorderStyle.None;
                    break;
                case DarkComboBox combo:
                    combo.BackColor = UiPalette.ButtonSurface;
                    combo.ForeColor = Text;
                    break;
                case SplitContainer split: split.BackColor = Border; break;
            }
        }
    }

    internal static void StyleButton(Button button, bool active = false)
    {
        button.UseVisualStyleBackColor = false;
        button.BackColor = active ? Accent : UiPalette.ButtonSurface;
        button.ForeColor = Text;
        if (button is RoundedButton rounded) { rounded.Active = active; return; }
        button.FlatStyle = FlatStyle.Flat;
        button.FlatAppearance.BorderSize = 0;
        button.FlatAppearance.MouseOverBackColor = active ? AccentHover : UiPalette.Hover;
        button.FlatAppearance.MouseDownBackColor = Selection;
    }

    internal static IEnumerable<Control> Descendants(Control root)
    {
        foreach (Control child in root.Controls)
        {
            yield return child;
            foreach (Control descendant in Descendants(child)) yield return descendant;
        }
    }
}
