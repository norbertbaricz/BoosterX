namespace BoosterX.App.Controls;

public class DarkCheckBox : CheckBox
{
    private bool hovered;
    public DarkCheckBox()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer
            | ControlStyles.ResizeRedraw | ControlStyles.Opaque, true);
        UseVisualStyleBackColor = false;
        ForeColor = UiPalette.Text;
        Cursor = Cursors.Hand;
    }

    protected override void OnPaintBackground(PaintEventArgs e)
    {
        // Paint only our own clipped surface. Replaying parent paint caused the corner trails.
        UiDrawing.ControlBackground(e.Graphics, ClientRectangle, Parent?.BackColor ?? UiPalette.Background);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        // CheckBox inherits the same opaque ButtonBase paint contract.
        UiDrawing.ControlBackground(e.Graphics, ClientRectangle, Parent?.BackColor ?? UiPalette.Background);
        Color text = Enabled ? UiPalette.Text : UiPalette.DisabledText;
        if (Appearance == Appearance.Button)
        {
            UiDrawing.FillRound(e.Graphics, ClientRectangle, LogicalToDeviceUnits(UiPalette.ControlRadius),
                !Enabled ? UiPalette.DisabledSurface : Checked ? UiPalette.Accent : hovered ? UiPalette.Hover : UiPalette.Card);
            UiDrawing.Text(e.Graphics, Text, Font, Rectangle.Inflate(ClientRectangle, -LogicalToDeviceUnits(8), -2), text,
                ContentAlignment.MiddleCenter, UseMnemonic);
            if (Enabled && Focused && ShowFocusCues) UiDrawing.FocusCue(e.Graphics, ClientRectangle, DeviceDpi);
            return;
        }
        int size = Math.Min(LogicalToDeviceUnits(16), Height - 2);
        Rectangle box = new(1, Math.Max(1, (Height - size) / 2), size, size);
        UiDrawing.Check(e.Graphics, box, CheckState, Enabled);
        Rectangle bounds = new(box.Right + LogicalToDeviceUnits(8), 0,
            Math.Max(1, Width - box.Right - LogicalToDeviceUnits(8)), Height);
        UiDrawing.Text(e.Graphics, Text, Font, bounds, text, ContentAlignment.MiddleLeft, UseMnemonic);
        if (Enabled && Focused && ShowFocusCues) UiDrawing.FocusCue(e.Graphics, new Rectangle(box.Left, 0, box.Width, Height), DeviceDpi, logicalInset: 0);
    }

    public override Size GetPreferredSize(Size proposedSize)
    {
        Size value = base.GetPreferredSize(proposedSize);
        return Appearance == Appearance.Button ? value : new(value.Width + LogicalToDeviceUnits(12), Math.Max(value.Height, LogicalToDeviceUnits(22)));
    }
    protected override void OnCheckedChanged(EventArgs e) { Invalidate(); base.OnCheckedChanged(e); }
    protected override void OnMouseEnter(EventArgs e) { hovered = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { hovered = false; Invalidate(); base.OnMouseLeave(e); }
    protected override void OnEnabledChanged(EventArgs e) { Invalidate(); base.OnEnabledChanged(e); }
    protected override void OnGotFocus(EventArgs e) { Invalidate(); base.OnGotFocus(e); }
    protected override void OnLostFocus(EventArgs e) { Invalidate(); base.OnLostFocus(e); }
}
