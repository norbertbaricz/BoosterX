using System.ComponentModel;

namespace BoosterX.App.Controls;

public sealed class DarkScrollBar : Control, IScrollWheelTarget
{
    private Orientation orientation = Orientation.Vertical;
    private int viewport = 1, extent = 1, value, smallChange = 1, dragStart, dragValue;
    private bool dragging, hovered;
    private WheelAccumulator wheel;
    [DefaultValue(Orientation.Vertical)] public Orientation Orientation { get => orientation; set { orientation = value; Invalidate(); } }
    [DefaultValue(0)] public int Value { get => value; set { int next = Math.Clamp(value, 0, Maximum); if (next == this.value) return; this.value = next; Invalidate(); ValueChanged?.Invoke(this, EventArgs.Empty); } }
    [DefaultValue(1)] public int SmallChange { get => smallChange; set => smallChange = Math.Max(1, value); }
    [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)] public int Maximum => Math.Max(0, extent - viewport);
    public event EventHandler? ValueChanged;

    public DarkScrollBar()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        Size = new Size(UiPalette.ScrollBarWidth, 120); AccessibleRole = AccessibleRole.ScrollBar; TabStop = false;
    }
    internal void Configure(int viewport, int extent, int offset)
    {
        this.viewport = Math.Max(0, viewport); this.extent = Math.Max(0, extent);
        value = Math.Clamp(offset, 0, Maximum); Invalidate();
    }
    private int Track => orientation == Orientation.Vertical ? Height : Width;
    private int Axis(Point point) => orientation == Orientation.Vertical ? point.Y : point.X;
    private ScrollGeometry Geometry => ScrollGeometry.Create(Track, viewport, extent, LogicalToDeviceUnits(UiPalette.MinimumThumb));
    protected override void OnPaintBackground(PaintEventArgs e)
    { using var brush = new SolidBrush(Parent?.BackColor ?? UiPalette.Surface); e.Graphics.FillRectangle(brush, ClientRectangle); }
    protected override void OnPaint(PaintEventArgs e)
    {
        ScrollGeometry geometry = Geometry;
        if (geometry.Range == 0 || Width < 2 || Height < 2) return;
        int position = geometry.Position(value);
        RectangleF thumb = orientation == Orientation.Vertical ? new(2, position, Math.Max(2, Width - 4), geometry.ThumbLength)
            : new(position, 2, geometry.ThumbLength, Math.Max(2, Height - 4));
        e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        using var path = UiDrawing.Round(thumb, Math.Min(thumb.Width, thumb.Height) / 2);
        using var fill = new SolidBrush(dragging ? UiPalette.Accent : hovered ? UiPalette.Muted : UiPalette.ScrollThumb);
        e.Graphics.FillPath(fill, path);
    }
    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e); if (e.Button != MouseButtons.Left) return;
        int point = Axis(e.Location); ScrollGeometry geometry = Geometry; int start = geometry.Position(value);
        if (point >= start && point <= start + geometry.ThumbLength) { dragging = true; dragStart = point; dragValue = start; Capture = true; }
        else Value += point < start ? -Math.Max(1, viewport) : Math.Max(1, viewport);
        Invalidate();
    }
    protected override void OnMouseMove(MouseEventArgs e) { base.OnMouseMove(e); if (dragging) Value = Geometry.Offset(dragValue + Axis(e.Location) - dragStart); }
    protected override void OnMouseUp(MouseEventArgs e) { dragging = false; Capture = false; Invalidate(); base.OnMouseUp(e); }
    protected override void OnMouseCaptureChanged(EventArgs e) { if (!Capture) dragging = false; Invalidate(); base.OnMouseCaptureChanged(e); }
    protected override void OnMouseEnter(EventArgs e) { hovered = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { hovered = false; Invalidate(); base.OnMouseLeave(e); }
    bool IScrollWheelTarget.ScrollWheel(WheelInput input)
        => Parent is IScrollWheelTarget owner ? owner.ScrollWheel(input with { Horizontal = input.Horizontal || orientation == Orientation.Horizontal })
            : ScrollOwn(input);
    internal bool ScrollOwn(WheelInput input)
    {
        if (input.Horizontal != (orientation == Orientation.Horizontal)) return false;
        WheelMove move = wheel.Move(Value, Maximum, input.Delta, SmallChange, viewport, input.Lines, input.Horizontal);
        if (move.Accepted) Value = move.Value;
        return move.Accepted;
    }
    protected override void OnMouseWheel(MouseEventArgs e)
    {
        bool consumed = ScrollWheelRouter.Dispatch(this, WheelInput.From(e));
        if (e is HandledMouseEventArgs handled) handled.Handled = consumed;
        if (!consumed) base.OnMouseWheel(e);
    }
}
