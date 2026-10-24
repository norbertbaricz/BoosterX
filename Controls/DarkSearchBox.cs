using System.ComponentModel;

namespace BoosterX.App.Controls;

/// <summary>Native text editing inside the same padded, 40px painted field as the selectors.</summary>
public sealed class DarkSearchBox : UserControl
{
    private readonly TextBox editor = new() { BorderStyle = BorderStyle.None };
    [DefaultValue(""),System.Diagnostics.CodeAnalysis.AllowNull]
    public override string Text { get => editor?.Text ?? ""; set { if (editor != null) editor.Text = value ?? ""; } }
    [DefaultValue("")] public string PlaceholderText { get => editor.PlaceholderText; set => editor.PlaceholderText = value ?? ""; }
    public DarkSearchBox()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.Opaque,true);
        Size = new(230,UiPalette.FieldHeight); MinimumSize = new(80,UiPalette.FieldHeight); BackColor = UiPalette.ButtonSurface;
        editor.BackColor = BackColor; editor.ForeColor = UiPalette.Text; Controls.Add(editor);
        editor.TextChanged += (_,_) => OnTextChanged(EventArgs.Empty);
        editor.Enter += (_,_) => Invalidate(); editor.Leave += (_,_) => Invalidate();
        Enter += (_,_) => editor.Focus(); TabStop = true;
    }
    protected override void OnLayout(LayoutEventArgs e)
    {
        base.OnLayout(e); if (editor == null) return;
        int pad = LogicalToDeviceUnits(12);
        editor.SetBounds(pad,Math.Max(0,(Height-editor.PreferredHeight)/2),Math.Max(1,Width-2*pad),editor.PreferredHeight);
    }
    protected override void OnFontChanged(EventArgs e) { base.OnFontChanged(e); if (editor != null) editor.Font = Font; PerformLayout(); }
    protected override void OnBackColorChanged(EventArgs e) { base.OnBackColorChanged(e); if (editor != null) editor.BackColor = BackColor; }
    protected override void OnForeColorChanged(EventArgs e) { base.OnForeColorChanged(e); if (editor != null) editor.ForeColor = ForeColor; }
    protected override void OnPaint(PaintEventArgs e)
    {
        UiDrawing.ControlBackground(e.Graphics,ClientRectangle,Parent?.BackColor ?? UiPalette.Background);
        UiDrawing.FillRound(e.Graphics,ClientRectangle,LogicalToDeviceUnits(UiPalette.ControlRadius),BackColor);
        if (ContainsFocus) UiDrawing.FocusCue(e.Graphics,ClientRectangle,DeviceDpi);
    }
}
