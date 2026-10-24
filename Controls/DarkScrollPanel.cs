using System.ComponentModel;

namespace BoosterX.App.Controls;

/// <summary>A clipped, two-axis viewport; all scrollbars are application-painted.</summary>
public sealed class DarkScrollPanel : UserControl, IScrollWheelTarget
{
    private readonly Panel viewport = new BufferedPanel();
    private readonly DarkScrollBar vertical = new() { SmallChange = UiPalette.PageWheelStep };
    private readonly DarkScrollBar horizontal = new() { Orientation = Orientation.Horizontal, SmallChange = UiPalette.PageWheelStep };
    private readonly HashSet<Control> boundControls = [];
    private Control? content;
    private bool arranging, queued, showVertical, showHorizontal;
    private int logicalMinimumWidth;
    private int logicalWheelStep = UiPalette.PageWheelStep;
    private WheelAccumulator wheel;
    [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Content)] public Panel Viewport => viewport;
    [DefaultValue(0)] public int LogicalMinimumContentWidth
    {
        get => logicalMinimumWidth;
        set { logicalMinimumWidth = Math.Max(0, value); Queue(); }
    }
    [DefaultValue(UiPalette.PageWheelStep)] public int LogicalWheelStep
    {
        get => logicalWheelStep;
        set { logicalWheelStep = Math.Max(1,value); Queue(); }
    }
    public DarkScrollPanel()
    {
        SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        BackColor = UiPalette.Background;
        ScrollWheelRouter.Watch(this);
        vertical.Visible = false; horizontal.Visible = false;
        Controls.Add(viewport); Controls.Add(vertical); Controls.Add(horizontal);
        viewport.ControlAdded += (_, e) =>
        {
            content = e.Control;
            if (content == null) return;
            content.Dock = DockStyle.None;
            if (content is ScrollableControl scroll) scroll.AutoScroll = false;
            content.AutoSize = false;
            content.Layout += (_, _) => Queue();
            BindWheel(content); Queue();
        };
        vertical.ValueChanged += (_, _) => { if (content != null && !arranging) content.Top = -vertical.Value; };
        horizontal.ValueChanged += (_, _) => { if (content != null && !arranging) content.Left = -horizontal.Value; };
        viewport.MouseWheel += Wheel;
    }
    private void BindWheel(Control control)
    {
        if (!boundControls.Add(control)) return;
        control.Enter += (_, _) => Reveal(control);
        if (control is DarkScrollPanel or DarkGridHost or DarkTextView) return;
        control.ControlAdded += (_, e) => { if (e.Control != null) BindWheel(e.Control); Queue(); };
        foreach (Control child in control.Controls) BindWheel(child);
    }
    private void Reveal(Control control)
    {
        if (arranging || content == null || !control.IsHandleCreated || !content.IsHandleCreated) return;
        Rectangle bounds = new(content.PointToClient(control.PointToScreen(Point.Empty)), control.Size);
        horizontal.Value = ResponsiveGeometry.Reveal(horizontal.Value, viewport.Width, bounds.Left, bounds.Width, content.Width);
        vertical.Value = ResponsiveGeometry.Reveal(vertical.Value, viewport.Height, bounds.Top, bounds.Height, content.Height);
    }
    private void Wheel(object? sender, MouseEventArgs e)
    {
        bool consumed = ScrollWheelRouter.Dispatch(this, WheelInput.From(e));
        if (e is HandledMouseEventArgs handled) handled.Handled = consumed;
    }
    bool IScrollWheelTarget.ScrollWheel(WheelInput input)
    {
        if (!Visible || content == null) return false;
        bool across = input.Horizontal || !showVertical && showHorizontal;
        DarkScrollBar bar = across ? horizontal : vertical;
        WheelMove move = wheel.Move(bar.Value, bar.Maximum, input.Delta, LogicalToDeviceUnits(logicalWheelStep),
            across ? viewport.Width : viewport.Height, input.Lines, across);
        if (move.Accepted) bar.Value = move.Value;
        return move.Accepted;
    }
    protected override void OnLayout(LayoutEventArgs e) { base.OnLayout(e); if (IsHandleCreated) ArrangeContent(); }
    protected override void OnSizeChanged(EventArgs e) { base.OnSizeChanged(e); Queue(); }
    protected override void OnVisibleChanged(EventArgs e) { base.OnVisibleChanged(e); if (Visible) Queue(); }
    protected override void OnHandleCreated(EventArgs e) { base.OnHandleCreated(e); Queue(); }
    protected override void OnDpiChangedAfterParent(EventArgs e) { base.OnDpiChangedAfterParent(e); Queue(); }
    private void Queue()
    {
        if (arranging || queued || !IsHandleCreated || IsDisposed || Disposing) return;
        queued = true;
        BeginInvoke((Action)(() => { queued = false; if (!IsDisposed && !Disposing) ArrangeContent(); }));
    }
    private void ArrangeContent()
    {
        if (arranging || content == null || Width < 1 || Height < 1 || !Visible) return;
        arranging = true;
        try
        {
            // Start without bars each time. A previous horizontal bar must not keep an unnecessary
            // vertical bar alive (and vice versa) after content shrinks or the window grows.
            bool needV = false, needH = false;
            int extentWidth = Width, extentHeight = Height;
            int offsetX = horizontal.Value, offsetY = vertical.Value;
            int bar = LogicalToDeviceUnits(UiPalette.ScrollBarWidth), gap = LogicalToDeviceUnits(UiPalette.ScrollBarGap);
            for (int pass = 0; pass < UiPalette.ScrollMeasurePasses; pass++)
            {
                Rectangle next = new(0, 0, Math.Max(1, Width - (needV ? bar + gap : 0)), Math.Max(1, Height - (needH ? bar + gap : 0)));
                if (viewport.Bounds != next) viewport.Bounds = next;
                extentWidth = Math.Max(viewport.Width, Math.Max(content.MinimumSize.Width, LogicalToDeviceUnits(logicalMinimumWidth)));
                // Measure against the current viewport, never the previous scroll extent.
                // Retaining an old tall height feeds surplus table-row space back into the scrollbar.
                Size probe = new(extentWidth, viewport.Height);
                if (content.Size != probe) content.Size = probe;
                content.PerformLayout();
                extentHeight = Math.Max(viewport.Height, RequiredHeight(content, extentWidth));
                bool measuredV = extentHeight > viewport.Height, measuredH = extentWidth > viewport.Width;
                if (measuredV == needV && measuredH == needH) break;
                needV = measuredV; needH = measuredH;
            }
            offsetX = Math.Clamp(offsetX, 0, Math.Max(0, extentWidth - viewport.Width));
            offsetY = Math.Clamp(offsetY, 0, Math.Max(0, extentHeight - viewport.Height));
            Rectangle bounds = new(-offsetX, -offsetY, extentWidth, extentHeight);
            if (content.Bounds != bounds) content.Bounds = bounds;
            vertical.Configure(viewport.Height, extentHeight, offsetY);
            horizontal.Configure(viewport.Width, extentWidth, offsetX);
            vertical.SmallChange = horizontal.SmallChange = LogicalToDeviceUnits(logicalWheelStep);
            showVertical = needV; showHorizontal = needH;
            vertical.Visible = needV; horizontal.Visible = needH;
            vertical.Bounds = new Rectangle(Math.Max(0, Width - bar), 0, bar, viewport.Height);
            horizontal.Bounds = new Rectangle(0, Math.Max(0, Height - bar), viewport.Width, bar);
        }
        finally { arranging = false; }
    }
    private static int RequiredHeight(Control control, int width)
    {
        if (control is not TableLayoutPanel table) return control.GetPreferredSize(new Size(width, 0)).Height;
        // GetRowHeights includes space assigned to the last row. For natural-height pages
        // (Repairs), ask for intrinsic content height instead of treating that surplus as content.
        if (!table.RowStyles.Cast<RowStyle>().Any(row => row.SizeType == SizeType.Percent && row.Height > 0))
            return Math.Max(table.MinimumSize.Height,table.GetPreferredSize(new Size(width,0)).Height);
        int fixedHeight = table.Padding.Vertical;
        int[] actual = table.GetRowHeights();
        var percentRows = new List<PercentMinimum>();
        for (int row = 0; row < table.RowCount; row++)
        {
            RowStyle style = row < table.RowStyles.Count ? table.RowStyles[row] : new RowStyle(SizeType.AutoSize);
            if (style.SizeType != SizeType.Percent) { fixedHeight += row < actual.Length ? actual[row] : 0; continue; }
            int minimum = 0;
            foreach (Control child in table.Controls)
                if (table.GetRow(child) == row && child.Visible) minimum = Math.Max(minimum, child.MinimumSize.Height + child.Margin.Vertical);
            if (style.Height > 0) percentRows.Add(new(style.Height, minimum)); else fixedHeight += minimum;
        }
        return ViewportGeometry.RequiredExtent(fixedHeight, percentRows);
    }
}
