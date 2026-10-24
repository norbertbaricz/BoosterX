using System.ComponentModel;

namespace BoosterX.App.Controls;

public enum CaptionGlyph { None, Minimize, Maximize, Restore, Close }

/// <summary>Filled, borderless buttons with standard Button input and accessibility behavior.</summary>
public class RoundedButton : Button
{
    private bool hovered;
    private bool pressed;
    private bool active;
    private CaptionGlyph glyph;
    private UiIcon symbol;
    private bool updatingAutoSize;

    [DefaultValue(false)]
    public bool Active { get => active; set { if (active != value) { active = value; Invalidate(); } } }
    [DefaultValue(CaptionGlyph.None)]
    public CaptionGlyph Glyph { get => glyph; set { glyph = value; Invalidate(); } }
    [DefaultValue(UiIcon.None)]
    public UiIcon Symbol
    {
        get => symbol;
        set { if (symbol == value) return; symbol = value; UpdateAutoSizeFloor(); Parent?.PerformLayout(this, nameof(Symbol)); Invalidate(); }
    }
    [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Color PaintedTextColor => Enabled ? UiPalette.Text : UiPalette.DisabledText;

    public RoundedButton()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer
            | ControlStyles.ResizeRedraw | ControlStyles.Opaque, true);
        FlatStyle = FlatStyle.Flat;
        FlatAppearance.BorderSize = 0;
        UseVisualStyleBackColor = false;
        ForeColor = UiPalette.Text;
        BackColor = UiPalette.ButtonSurface;
        Cursor = Cursors.Hand;
    }

    private Color SurfaceColor()
    {
        if (!Enabled) return UiPalette.DisabledSurface;
        if (active) return pressed ? UiPalette.AccentPressed : hovered ? UiPalette.AccentHover : UiPalette.Accent;
        if (glyph == CaptionGlyph.Close && (hovered || pressed)) return pressed ? UiPalette.AccentPressed : UiPalette.Accent;
        if (pressed) return UiPalette.Selection;
        if (hovered) return UiPalette.Hover;
        return glyph != CaptionGlyph.None ? Parent?.BackColor ?? UiPalette.Background : BackColor;
    }
    protected override void OnPaintBackground(PaintEventArgs e)
    {
        UiDrawing.ControlBackground(e.Graphics, ClientRectangle, Parent?.BackColor ?? UiPalette.Background);
    }
    protected override void OnPaint(PaintEventArgs e)
    {
        // ButtonBase sets Opaque: WM_PAINT may skip OnPaintBackground entirely.
        // Every foreground pass must cover the whole invalidated area, including round corners.
        UiDrawing.ControlBackground(e.Graphics, ClientRectangle, Parent?.BackColor ?? UiPalette.Background);
        if (Width < 2 || Height < 2) return;
        UiDrawing.FillRound(e.Graphics, ClientRectangle, LogicalToDeviceUnits(UiPalette.ControlRadius), SurfaceColor());
        if (glyph != CaptionGlyph.None) PaintGlyph(e.Graphics, PaintedTextColor);
        else
        {
            Rectangle content = Rectangle.FromLTRB(Padding.Left, Padding.Top,
                Math.Max(Padding.Left + 1, Width - Padding.Right), Math.Max(Padding.Top + 1, Height - Padding.Bottom));
            if (symbol != UiIcon.None)
            {
                int alignment = TextAlign is ContentAlignment.TopLeft or ContentAlignment.MiddleLeft or ContentAlignment.BottomLeft ? -1
                    : TextAlign is ContentAlignment.TopRight or ContentAlignment.MiddleRight or ContentAlignment.BottomRight ? 1 : 0;
                IconContent parts = IconGeometry.Arrange(content, UiDrawing.MeasureButtonText(Text, Font, UseMnemonic),
                    LogicalToDeviceUnits(UiPalette.ActionIconSize), LogicalToDeviceUnits(UiPalette.ActionIconGap), alignment);
                UiIcons.Draw(e.Graphics, parts.Icon, symbol, PaintedTextColor);
                content = parts.Text;
            }
            if (content.Width > 0 && content.Height > 0)
                TextRenderer.DrawText(e.Graphics, Text, Font, content, PaintedTextColor, UiDrawing.ButtonTextFlags(TextAlign, UseMnemonic));
        }
        if (Enabled && Focused && ShowFocusCues) UiDrawing.FocusCue(e.Graphics, ClientRectangle, DeviceDpi);
    }
    private Size MeasureContent()
    {
        Size text = UiDrawing.MeasureButtonText(Text, Font, UseMnemonic);
        if (symbol != UiIcon.None) text = IconGeometry.Measure(text, LogicalToDeviceUnits(UiPalette.ActionIconSize), LogicalToDeviceUnits(UiPalette.ActionIconGap));
        return new Size(text.Width + Padding.Horizontal + LogicalToDeviceUnits(UiPalette.ButtonTextSafety),
            Math.Max(LogicalToDeviceUnits(UiPalette.ButtonMinimumHeight), text.Height + Padding.Vertical));
    }
    public override Size GetPreferredSize(Size proposedSize)
    {
        if (glyph != CaptionGlyph.None) return base.GetPreferredSize(proposedSize);
        Size content = MeasureContent();
        return new Size(Math.Max(MinimumSize.Width, content.Width), Math.Max(MinimumSize.Height, content.Height));
    }
    private void UpdateAutoSizeFloor()
    {
        if (!AutoSize || glyph != CaptionGlyph.None || updatingAutoSize || string.IsNullOrEmpty(Text)) return;
        updatingAutoSize = true;
        try
        {
            // The floor also protects labels when the inherited ButtonBase preferred-size cache is used.
            Size desired = GetPreferredSize(Size.Empty);
            if (MinimumSize != desired) MinimumSize = desired;
        }
        finally { updatingAutoSize = false; }
    }
    private void PaintGlyph(Graphics graphics, Color color)
    {
        float span = LogicalToDeviceUnits(UiPalette.CaptionGlyphSize), x = (Width - span) / 2f, y = (Height - span) / 2f;
        using var pen = new Pen(color, Math.Max(1.2f, DeviceDpi / 96f));
        if (glyph == CaptionGlyph.Minimize) graphics.DrawLine(pen, x, y + span * 0.8f, x + span, y + span * 0.8f);
        else if (glyph == CaptionGlyph.Maximize) graphics.DrawRectangle(pen, x, y, span, span);
        else if (glyph == CaptionGlyph.Restore)
        {
            graphics.DrawLines(pen, [new PointF(x + span * 0.3f, y), new PointF(x + span, y), new PointF(x + span, y + span * 0.7f)]);
            graphics.DrawRectangle(pen, x, y + span * 0.3f, span * 0.7f, span * 0.7f);
        }
        else { graphics.DrawLine(pen, x, y, x + span, y + span); graphics.DrawLine(pen, x + span, y, x, y + span); }
    }
    protected override void OnMouseEnter(EventArgs e) { hovered = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { hovered = false; pressed = false; Invalidate(); base.OnMouseLeave(e); }
    protected override void OnMouseDown(MouseEventArgs e) { if (e.Button == MouseButtons.Left) pressed = true; Invalidate(); base.OnMouseDown(e); }
    protected override void OnMouseUp(MouseEventArgs e) { pressed = false; Invalidate(); base.OnMouseUp(e); }
    protected override void OnMouseCaptureChanged(EventArgs e) { if (!Capture) { pressed = false; Invalidate(); } base.OnMouseCaptureChanged(e); }
    protected override void OnKeyDown(KeyEventArgs e) { if (e.KeyCode == Keys.Space) pressed = true; Invalidate(); base.OnKeyDown(e); }
    protected override void OnKeyUp(KeyEventArgs e) { pressed = false; Invalidate(); base.OnKeyUp(e); }
    protected override void OnEnabledChanged(EventArgs e) { pressed = false; hovered = false; Invalidate(); base.OnEnabledChanged(e); }
    protected override void OnGotFocus(EventArgs e) { Invalidate(); base.OnGotFocus(e); }
    protected override void OnLostFocus(EventArgs e) { pressed = false; Invalidate(); base.OnLostFocus(e); }
    protected override void OnTextChanged(EventArgs e) { base.OnTextChanged(e); UpdateAutoSizeFloor(); Invalidate(); }
    protected override void OnFontChanged(EventArgs e) { base.OnFontChanged(e); UpdateAutoSizeFloor(); Invalidate(); }
    protected override void OnPaddingChanged(EventArgs e) { base.OnPaddingChanged(e); UpdateAutoSizeFloor(); Invalidate(); }
    protected override void OnAutoSizeChanged(EventArgs e) { base.OnAutoSizeChanged(e); UpdateAutoSizeFloor(); }
    protected override void OnParentBackColorChanged(EventArgs e) { base.OnParentBackColorChanged(e); Invalidate(); }
}
