using System.ComponentModel;

namespace BoosterX.App.Controls;

public class DarkDataGridView : DataGridView
{
    public DarkDataGridView() { DoubleBuffered = true; ScrollBars = ScrollBars.None; }
    protected override void OnMouseWheel(MouseEventArgs e)
    {
        bool consumed = ScrollWheelRouter.Dispatch(this, WheelInput.From(e));
        if (e is HandledMouseEventArgs handled) handled.Handled = consumed;
        if (!consumed) base.OnMouseWheel(e);
    }
}

public sealed class DarkGridHost : UserControl, IScrollWheelTarget
{
    private readonly DarkScrollBar vertical = new();
    private readonly DarkScrollBar horizontal = new() { Orientation = Orientation.Horizontal };
    private readonly Panel viewport = new BufferedPanel() { Margin = Padding.Empty };
    private readonly List<ScrollRow> rows = [];
    private DarkDataGridView? grid;
    private bool updating, queued, rowsDirty = true;
    private int rowViewport = 1, columnViewport = 1, frozenHeight;
    private WheelAccumulator wheel;
    [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Content)] public Panel Viewport => viewport;

    public DarkGridHost()
    {
        BackColor = UiPalette.Surface;
        SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        ScrollWheelRouter.Watch(this);
        vertical.Visible = false; horizontal.Visible = false;
        Controls.Add(viewport); Controls.Add(vertical); Controls.Add(horizontal);
        viewport.ControlAdded += (_, e) =>
        {
            if (e.Control is not DarkDataGridView value) return;
            grid = value; grid.Dock = DockStyle.Fill; grid.MinimumSize = Size.Empty; grid.ScrollBars = ScrollBars.None;
            grid.Scroll += (_, _) => Queue();
            grid.DataBindingComplete += (_, _) => RowsChanged();
            grid.RowsAdded += (_, _) => RowsChanged(); grid.RowsRemoved += (_, _) => RowsChanged();
            grid.RowHeightChanged += (_, _) => RowsChanged(); grid.Sorted += (_, _) => RowsChanged();
            grid.RowStateChanged += (_, args) => { if ((args.StateChanged & (DataGridViewElementStates.Visible | DataGridViewElementStates.Frozen)) != 0) RowsChanged(); };
            grid.ColumnWidthChanged += (_, _) => Queue(); grid.ColumnStateChanged += (_, _) => Queue();
            grid.ColumnAdded += (_, _) => Queue(); grid.ColumnRemoved += (_, _) => Queue();
            grid.ColumnHeadersHeightChanged += (_, _) => Queue(); grid.RowHeadersWidthChanged += (_, _) => Queue();
            grid.KeyUp += (_, _) => Queue(); grid.VisibleChanged += (_, _) => Queue();
            RowsChanged();
        };
        vertical.ValueChanged += (_, _) => ApplyVertical();
        horizontal.ValueChanged += (_, _) =>
        {
            if (updating || grid == null || grid.IsDisposed) return;
            grid.HorizontalScrollingOffset = horizontal.Value; Queue();
        };
    }
    private void RowsChanged() { rowsDirty = true; Queue(); }
    private void ReadRows()
    {
        if (!rowsDirty || grid == null) return;
        rowsDirty = false; rows.Clear(); frozenHeight = 0;
        // SharedRow/GetRowState avoid materializing every row in large cleanup previews.
        for (int i = grid.Rows.GetFirstRow(DataGridViewElementStates.Visible); i >= 0;
            i = grid.Rows.GetNextRow(i, DataGridViewElementStates.Visible))
        {
            int height = Math.Max(1, grid.Rows.SharedRow(i).Height);
            if ((grid.Rows.GetRowState(i) & DataGridViewElementStates.Frozen) != 0)
                frozenHeight = (int)Math.Min(int.MaxValue, (long)frozenHeight + height);
            else rows.Add(new(i, height));
        }
    }
    private void ApplyVertical()
    {
        if (updating || grid == null || grid.IsDisposed) return;
        ReadRows();
        int target = GridScrollGeometry.RowAt(rows, vertical.Value, rowViewport);
        if (target < 0 || target >= grid.Rows.Count) return;
        if (grid.IsCurrentCellInEditMode && !grid.EndEdit()) return;
        grid.FirstDisplayedScrollingRowIndex = target;
        Queue();
    }
    bool IScrollWheelTarget.ScrollWheel(WheelInput input)
    {
        if (grid == null || !grid.Visible || grid.IsDisposed) return false;
        ReadRows();
        bool across = input.Horizontal || !vertical.Visible && horizontal.Visible;
        DarkScrollBar bar = across ? horizontal : vertical;
        int first = grid.FirstDisplayedScrollingRowIndex;
        if (!across)
        {
            if (bar.Maximum == 0 || bar.Value == 0 && input.Delta > 0 || bar.Value == bar.Maximum && input.Delta < 0)
            { wheel.Move(0, 0, input.Delta, 1, 1, input.Lines, false); return false; }
            int current = rows.FindIndex(row => row.Index == first);
            int lastIndex = GridScrollGeometry.RowAt(rows, int.MaxValue, rowViewport);
            int last = rows.FindIndex(row => row.Index == lastIndex);
            WheelMove rowMove = wheel.Move(Math.Max(0, current), Math.Max(0, last), input.Delta, 1,
                Math.Max(1, grid.DisplayedRowCount(false)), input.Lines, false);
            if (rowMove.Accepted && rowMove.Value != current)
            {
                if (grid.IsCurrentCellInEditMode && !grid.EndEdit()) return true;
                grid.FirstDisplayedScrollingRowIndex = rows[rowMove.Value].Index; Queue();
                if (grid.FirstDisplayedScrollingRowIndex == first) return false;
            }
            return rowMove.Accepted;
        }
        WheelMove move = wheel.Move(bar.Value, bar.Maximum, input.Delta, LogicalToDeviceUnits(UiPalette.HorizontalWheelStep),
            columnViewport, input.Lines, true);
        if (move.Accepted) horizontal.Value = move.Value;
        return move.Accepted;
    }
    protected override void OnLayout(LayoutEventArgs e) { base.OnLayout(e); if (IsHandleCreated) Arrange(); }
    protected override void OnSizeChanged(EventArgs e) { base.OnSizeChanged(e); Queue(); }
    protected override void OnDpiChangedAfterParent(EventArgs e) { base.OnDpiChangedAfterParent(e); RowsChanged(); }
    protected override void OnVisibleChanged(EventArgs e) { base.OnVisibleChanged(e); if (Visible) Queue(); }
    protected override void OnHandleCreated(EventArgs e) { base.OnHandleCreated(e); Queue(); }
    private void Queue()
    {
        if (updating || !IsHandleCreated || queued || IsDisposed || Disposing) return;
        queued = true; BeginInvoke((Action)(() => { queued = false; if (!IsDisposed && !Disposing) Arrange(); }));
    }
    private void Arrange()
    {
        if (updating || grid == null || grid.IsDisposed || Width < 1 || Height < 1 || !Visible) return;
        updating = true;
        try
        {
            int bar = LogicalToDeviceUnits(UiPalette.ScrollBarWidth), gap = LogicalToDeviceUnits(UiPalette.ScrollBarGap);
            bool needV = false, needH = false;
            for (int pass = 0; pass < UiPalette.ScrollMeasurePasses; pass++)
            {
                viewport.Bounds = new(0, 0, Math.Max(1, Width - (needV ? bar + gap : 0)), Math.Max(1, Height - (needH ? bar + gap : 0)));
                viewport.PerformLayout(); grid.PerformLayout(); ReadRows();
                int borderX = grid.BorderStyle == BorderStyle.None ? 0 : grid.BorderStyle == BorderStyle.Fixed3D ? SystemInformation.Border3DSize.Width * 2 : SystemInformation.BorderSize.Width * 2;
                int borderY = grid.BorderStyle == BorderStyle.None ? 0 : grid.BorderStyle == BorderStyle.Fixed3D ? SystemInformation.Border3DSize.Height * 2 : SystemInformation.BorderSize.Height * 2;
                int frozenWidth = grid.Columns.GetColumnsWidth(DataGridViewElementStates.Visible | DataGridViewElementStates.Frozen);
                rowViewport = Math.Max(1, grid.ClientSize.Height - borderY - frozenHeight - (grid.ColumnHeadersVisible ? grid.ColumnHeadersHeight : 0));
                columnViewport = Math.Max(1, grid.ClientSize.Width - borderX - frozenWidth - (grid.RowHeadersVisible ? grid.RowHeadersWidth : 0));
                int extent = GridScrollGeometry.Extent(rows);
                int columns = Math.Max(0, grid.Columns.GetColumnsWidth(DataGridViewElementStates.Visible) - frozenWidth);
                bool measuredV = extent > rowViewport, measuredH = columns > columnViewport;
                vertical.SmallChange = Math.Max(1, grid.RowTemplate.Height);
                horizontal.SmallChange = LogicalToDeviceUnits(UiPalette.HorizontalWheelStep);
                vertical.Configure(rowViewport, extent, GridScrollGeometry.Offset(rows, grid.FirstDisplayedScrollingRowIndex));
                horizontal.Configure(columnViewport, columns, grid.HorizontalScrollingOffset);
                if (measuredV == needV && measuredH == needH) break;
                needV = measuredV; needH = measuredH;
            }
            vertical.Visible = needV; horizontal.Visible = needH;
            vertical.Bounds = new(Math.Max(0, Width - bar), 0, bar, viewport.Height);
            horizontal.Bounds = new(0, Math.Max(0, Height - bar), viewport.Width, bar);
        }
        finally { updating = false; }
    }
}
