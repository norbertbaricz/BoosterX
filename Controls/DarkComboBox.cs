using System.Collections;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Drawing.Drawing2D;
using System.Globalization;

namespace BoosterX.App.Controls;

/// <summary>A labelled selector with a fully painted dark popup and the shared custom scrollbar.</summary>
[DefaultEvent(nameof(SelectedIndexChanged))]
public sealed class DarkComboBox : Control
{
    private readonly ObjectCollection items;
    private object? dataSource;
    private int selectedIndex = -1, itemHeight = UiPalette.DropDownItemHeight;
    private string displayMember = "", placeholder = "Select an option";
    private UiIcon symbol;
    private bool hovered;
    private ToolStripDropDown? popup;
    private ToolStripControlHost? popupHost;
    private DropDownList? list;
    private Form? popupOwner;
    private string search = "";
    private long lastSearch;

    public event EventHandler? SelectedIndexChanged;
    public event EventHandler? SelectionChangeCommitted;

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Content)] public ObjectCollection Items => items;
    [DefaultValue(null)] public object? DataSource
    {
        get => dataSource;
        set
        {
            if (value != null && (value is not IEnumerable || value is string)) throw new ArgumentException("DataSource must be a collection.", nameof(value));
            object[] values = value is IEnumerable source ? source.Cast<object>().ToArray() : [];
            CloseDropDown(); dataSource = value; items.Replace(values);
            SelectedIndex = values.Length > 0 ? 0 : -1;
        }
    }
    [DefaultValue("")] public string DisplayMember { get => displayMember; set { displayMember = value ?? ""; Invalidate(); list?.Invalidate(); } }
    [DefaultValue("Select an option")] public string PlaceholderText { get => placeholder; set { placeholder = value ?? ""; Invalidate(); } }
    [DefaultValue(UiIcon.None)] public UiIcon Symbol { get => symbol; set { symbol = value; Invalidate(); } }
    [DefaultValue(UiPalette.DropDownItemHeight)] public int ItemHeight { get => itemHeight; set { itemHeight = Math.Max(1, value); CloseDropDown(); } }
    [DefaultValue(-1)] public int SelectedIndex
    {
        get => selectedIndex;
        set
        {
            if (value < -1 || value >= items.Count) throw new ArgumentOutOfRangeException(nameof(value));
            if (selectedIndex == value) return;
            selectedIndex = value; Invalidate();
            AccessibilityNotifyClients(AccessibleEvents.ValueChange, -1);
            SelectedIndexChanged?.Invoke(this, EventArgs.Empty);
        }
    }
    [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public object? SelectedItem { get => selectedIndex >= 0 && selectedIndex < items.Count ? items[selectedIndex] : null; set => SelectedIndex = value == null ? -1 : items.IndexOf(value); }
    [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)] public bool DroppedDown => popup?.Visible == true;
    internal int EffectiveItemHeight => Math.Max(itemHeight, Font.Height + LogicalToDeviceUnits(UiPalette.DropDownItemVerticalPadding));
    internal DropDownList? PopupList => list;

    public DarkComboBox()
    {
        items = new ObjectCollection(this);
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer
            | ControlStyles.ResizeRedraw | ControlStyles.Opaque | ControlStyles.Selectable, true);
        BackColor = UiPalette.ButtonSurface; ForeColor = UiPalette.Text;
        Size = new Size(UiPalette.DropDownMinimumWidth, UiPalette.DropDownMinimumHeight);
        MinimumSize = new Size(0, UiPalette.DropDownMinimumHeight);
        TabStop = true; Cursor = Cursors.Hand; AccessibleRole = AccessibleRole.ComboBox;
    }
    public string GetItemText(object? item)
    {
        if (item == null) return "";
        if (displayMember.Length > 0) item = TypeDescriptor.GetProperties(item)[displayMember]?.GetValue(item);
        return Convert.ToString(item, CultureInfo.CurrentCulture) ?? "";
    }
    protected override AccessibleObject CreateAccessibilityInstance() => new SelectorAccessibleObject(this);
    public override Size GetPreferredSize(Size proposedSize) => new(Math.Max(MinimumSize.Width, LogicalToDeviceUnits(UiPalette.DropDownMinimumWidth)),
        Math.Max(LogicalToDeviceUnits(UiPalette.DropDownMinimumHeight), Font.Height + LogicalToDeviceUnits(14)));
    protected override void OnPaint(PaintEventArgs e)
    {
        UiDrawing.ControlBackground(e.Graphics, ClientRectangle, Parent?.BackColor ?? UiPalette.Background);
        UiDrawing.FillRound(e.Graphics, ClientRectangle, LogicalToDeviceUnits(UiPalette.ControlRadius),
            !Enabled ? UiPalette.DisabledSurface : DroppedDown ? UiPalette.Selection : hovered ? UiPalette.Hover : BackColor);
        Color foreground = Enabled ? ForeColor : UiPalette.DisabledText;
        int inset = LogicalToDeviceUnits(12), arrowWidth = LogicalToDeviceUnits(UiPalette.DropDownArrowWidth);
        Rectangle content = new(inset, 0, Math.Max(0, Width - inset - arrowWidth), Height);
        if (symbol != UiIcon.None)
        {
            int side = LogicalToDeviceUnits(UiPalette.ActionIconSize), gap = LogicalToDeviceUnits(UiPalette.ActionIconGap);
            UiIcons.Draw(e.Graphics, new Rectangle(content.Left, (Height - side) / 2, side, side), symbol, foreground);
            content.X += side + gap; content.Width = Math.Max(0, content.Width - side - gap);
        }
        TextRenderer.DrawText(e.Graphics, SelectedItem != null ? GetItemText(SelectedItem) : placeholder, Font, content,
            SelectedItem != null ? foreground : Enabled ? UiPalette.Muted : UiPalette.DisabledText,
            UiDrawing.ButtonTextFlags(ContentAlignment.MiddleLeft, false));
        if (Width >= arrowWidth)
        {
            var saved = e.Graphics.Save();
            try
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using var pen = new Pen(foreground, Math.Max(1.5f, DeviceDpi / 64f)) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round };
                float x = Width - arrowWidth / 2f, y = Height / 2f, span = LogicalToDeviceUnits(4);
                e.Graphics.DrawLines(pen, DroppedDown ? [new PointF(x-span, y+span/2), new PointF(x, y-span/2), new PointF(x+span, y+span/2)]
                    : [new PointF(x-span, y-span/2), new PointF(x, y+span/2), new PointF(x+span, y-span/2)]);
            }
            finally { e.Graphics.Restore(saved); }
        }
        if (Focused && ShowFocusCues && Enabled) UiDrawing.FocusCue(e.Graphics, ClientRectangle, DeviceDpi);
    }
    protected override void OnMouseDown(MouseEventArgs e) { base.OnMouseDown(e); if (e.Button == MouseButtons.Left && Enabled) { Focus(); ToggleDropDown(); } }
    protected override void OnMouseEnter(EventArgs e) { hovered = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { hovered = false; Invalidate(); base.OnMouseLeave(e); }
    protected override void OnGotFocus(EventArgs e) { Invalidate(); base.OnGotFocus(e); }
    protected override void OnLostFocus(EventArgs e) { Invalidate(); base.OnLostFocus(e); }
    protected override void OnEnabledChanged(EventArgs e) { if (!Enabled) CloseDropDown(); Invalidate(); base.OnEnabledChanged(e); }
    protected override void OnVisibleChanged(EventArgs e) { if (!Visible) CloseDropDown(); base.OnVisibleChanged(e); }
    protected override void OnFontChanged(EventArgs e) { base.OnFontChanged(e); CloseDropDown(); Invalidate(); }
    protected override void OnDpiChangedAfterParent(EventArgs e)
    { base.OnDpiChangedAfterParent(e); CloseDropDown(); MinimumSize = new(MinimumSize.Width, LogicalToDeviceUnits(UiPalette.DropDownMinimumHeight)); }
    protected override bool IsInputKey(Keys keyData) => (keyData & Keys.KeyCode) is Keys.Up or Keys.Down or Keys.Home or Keys.End or Keys.PageUp or Keys.PageDown || base.IsInputKey(keyData);
    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.KeyCode == Keys.F4 || e.Alt && e.KeyCode is Keys.Down or Keys.Up || e.KeyCode == Keys.Space)
        { ToggleDropDown(); e.Handled = e.SuppressKeyPress = true; }
        else if (HandleNavigation(e.KeyCode, commit: true)) e.Handled = e.SuppressKeyPress = true;
        base.OnKeyDown(e);
    }
    protected override void OnKeyPress(KeyPressEventArgs e) { if (!char.IsControl(e.KeyChar)) { TypeSearch(e.KeyChar, false); e.Handled = true; } base.OnKeyPress(e); }
    internal bool HandleNavigation(Keys key, bool commit)
    {
        if (items.Count == 0) return false;
        int current = !commit && list != null ? list.HighlightedIndex : selectedIndex;
        int page = Math.Max(1, list?.VisibleRows ?? UiPalette.DropDownMaxVisibleItems);
        int next = key switch
        {
            Keys.Up => current < 0 ? items.Count - 1 : current - 1,
            Keys.Down => current + 1,
            Keys.Home => 0, Keys.End => items.Count - 1,
            Keys.PageUp => current - page, Keys.PageDown => current + page,
            _ => -2
        };
        if (next == -2) return false;
        next = Math.Clamp(next, 0, items.Count - 1);
        if (commit) Commit(next, close: false); else list?.Highlight(next);
        return true;
    }
    internal void TypeSearch(char character, bool preview)
    {
        long now = Environment.TickCount64;
        if (now - lastSearch > UiPalette.DropDownTypeSearchMilliseconds) search = "";
        search += character; lastSearch = now;
        int Find(string prefix)
        {
            for (int i = 0; i < items.Count; i++)
                if (GetItemText(items[i]).StartsWith(prefix, StringComparison.CurrentCultureIgnoreCase)) return i;
            return -1;
        }
        int match = Find(search);
        if (match < 0 && search.Length > 1) { search = character.ToString(); match = Find(search); }
        if (match >= 0) { if (preview) list?.Highlight(match); else Commit(match, close: false); }
    }
    internal void Commit(int index, bool close = true)
    {
        SelectedIndex = index; SelectionChangeCommitted?.Invoke(this, EventArgs.Empty);
        if (close) { CloseDropDown(); Focus(); }
    }
    public void ToggleDropDown() { if (DroppedDown) CloseDropDown(); else OpenDropDown(); }
    public void OpenDropDown()
    {
        if (DroppedDown || !Enabled || !Visible || items.Count == 0 || !IsHandleCreated) return;
        int padding = LogicalToDeviceUnits(UiPalette.DropDownPadding);
        int width = Math.Max(Width, LogicalToDeviceUnits(UiPalette.DropDownMinimumWidth));
        foreach (object item in items)
            width = Math.Max(width, UiDrawing.MeasureButtonText(GetItemText(item), Font, false).Width
                + 2 * padding + LogicalToDeviceUnits(42));
        width = Math.Min(width, LogicalToDeviceUnits(UiPalette.DropDownMaximumWidth));
        Rectangle anchor = RectangleToScreen(ClientRectangle);
        PopupPlacement placement = DropDownGeometry.Place(anchor, Screen.FromControl(this).WorkingArea, width,
            EffectiveItemHeight, items.Count, padding, UiPalette.DropDownMaxVisibleItems);
        // Keep one popup for this selector's lifetime. ToolStrip continues using its window
        // after Closed returns; disposing it from Closed caused the 4.8 selection crash.
        if (popup == null)
        {
            list = new DropDownList(this) { Font = Font, Margin = Padding.Empty };
            popupHost = new ToolStripControlHost(list) { AutoSize = false, Margin = Padding.Empty, Padding = Padding.Empty };
            popup = new ToolStripDropDown { AutoSize = false, Padding = Padding.Empty, Margin = Padding.Empty,
                BackColor = UiPalette.Card, ForeColor = UiPalette.Text, Renderer = new DarkMenuRenderer(), AutoClose = true };
            popup.Items.Add(popupHost);
            popup.Closed += PopupClosed;
        }
        if (list == null || popupHost == null || popup.IsDisposed) return;
        search = ""; lastSearch = 0;
        list.Font = Font; list.Size = placement.Bounds.Size;
        popupHost.Size = placement.Bounds.Size; popup.Size = placement.Bounds.Size;
        popupOwner = FindForm();
        if (popupOwner != null) { popupOwner.LocationChanged += OwnerMoved; popupOwner.Resize += OwnerMoved; }
        list.Highlight(selectedIndex >= 0 ? selectedIndex : 0);
        popup.Show(placement.Bounds.Location);
        if (!popup.Visible) return;
        // The popup now has its monitor DPI. Apply device-pixel bounds after native creation,
        // so ToolStrip's initial DPI scaling cannot enlarge an already-scaled placement.
        popup.Bounds = placement.Bounds; popupHost.Size = placement.Bounds.Size; list.Size = placement.Bounds.Size;
        list.Focus(); Invalidate();
        AccessibilityNotifyClients(AccessibleEvents.StateChange, -1);
    }
    private void OwnerMoved(object? sender, EventArgs e) => CloseDropDown();
    public void CloseDropDown() => popup?.Close(ToolStripDropDownCloseReason.CloseCalled);
    private void PopupClosed(object? sender, ToolStripDropDownClosedEventArgs e)
    {
        if (popupOwner != null) { popupOwner.LocationChanged -= OwnerMoved; popupOwner.Resize -= OwnerMoved; popupOwner = null; }
        Invalidate(); if (IsHandleCreated && !IsDisposed) AccessibilityNotifyClients(AccessibleEvents.StateChange, -1);
    }
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            if (popupOwner != null) { popupOwner.LocationChanged -= OwnerMoved; popupOwner.Resize -= OwnerMoved; popupOwner = null; }
            if (popup != null) { popup.Closed -= PopupClosed; popup.Close(); popup.Dispose(); popup = null; }
            popupHost = null; list = null;
        }
        base.Dispose(disposing);
    }
    private void ItemsChanged()
    {
        if (selectedIndex >= items.Count) SelectedIndex = -1;
        Invalidate(); CloseDropDown();
    }
    public sealed class ObjectCollection : Collection<object>
    {
        private readonly DarkComboBox owner;
        internal ObjectCollection(DarkComboBox owner) => this.owner = owner;
        public void AddRange(object[] values) { foreach (object value in values) Add(value); }
        internal void Replace(object[] values)
        {
            base.ClearItems(); foreach (object value in values) base.InsertItem(Count, value);
            owner.ItemsChanged();
        }
        protected override void InsertItem(int index, object item) { base.InsertItem(index, item); owner.ItemsChanged(); }
        protected override void SetItem(int index, object item) { base.SetItem(index, item); owner.ItemsChanged(); }
        protected override void RemoveItem(int index) { base.RemoveItem(index); owner.ItemsChanged(); }
        protected override void ClearItems() { base.ClearItems(); owner.ItemsChanged(); }
    }
    private sealed class SelectorAccessibleObject(DarkComboBox owner) : ControlAccessibleObject(owner)
    {
        public override AccessibleRole Role => AccessibleRole.ComboBox;
        public override string? Value { get => owner.GetItemText(owner.SelectedItem); set { } }
        public override string? DefaultAction => owner.DroppedDown ? "Collapse" : "Expand";
        public override AccessibleStates State => base.State | (owner.DroppedDown ? AccessibleStates.Expanded : AccessibleStates.Collapsed);
        public override void DoDefaultAction() => owner.ToggleDropDown();
        public override int GetChildCount() => owner.DroppedDown ? 1 : 0;
        public override AccessibleObject? GetChild(int index) => index == 0 && owner.DroppedDown ? owner.list?.AccessibilityObject : null;
    }

    internal sealed class DropDownList : Control, IScrollWheelTarget
    {
        private readonly DarkComboBox owner;
        private readonly DarkScrollBar scroll = new();
        internal int HighlightedIndex { get; private set; } = -1;
        internal int VisibleRows => Math.Max(1, ContentBounds.Height / owner.EffectiveItemHeight);
        internal int ScrollOffset => scroll.Value;
        private Rectangle ContentBounds
        {
            get
            {
                int pad = LogicalToDeviceUnits(UiPalette.DropDownPadding);
                return new(pad, pad, Math.Max(1, Width - 2 * pad - (scroll.Visible ? scroll.Width + LogicalToDeviceUnits(UiPalette.ScrollBarGap) : 0)), Math.Max(1, Height - 2 * pad));
            }
        }
        internal DropDownList(DarkComboBox owner)
        {
            this.owner = owner; BackColor = UiPalette.Card; ForeColor = UiPalette.Text; TabStop = true;
            AccessibleRole = AccessibleRole.List; AccessibleName = owner.AccessibleName + " options";
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer
                | ControlStyles.ResizeRedraw | ControlStyles.Opaque | ControlStyles.Selectable, true);
            Controls.Add(scroll); scroll.ValueChanged += (_, _) => Invalidate();
            ScrollWheelRouter.Watch(this);
        }
        protected override AccessibleObject CreateAccessibilityInstance() => new ListAccessibleObject(this);
        protected override void OnLayout(LayoutEventArgs e)
        {
            base.OnLayout(e);
            int pad = LogicalToDeviceUnits(UiPalette.DropDownPadding), height = Math.Max(1, Height - 2 * pad);
            int extent = (int)Math.Min(int.MaxValue, (long)owner.items.Count * owner.EffectiveItemHeight);
            scroll.Visible = extent > height;
            scroll.Bounds = new(Width - pad - LogicalToDeviceUnits(UiPalette.ScrollBarWidth), pad, LogicalToDeviceUnits(UiPalette.ScrollBarWidth), height);
            scroll.SmallChange = owner.EffectiveItemHeight; scroll.Configure(height, extent, scroll.Value);
        }
        internal void Highlight(int index)
        {
            HighlightedIndex = Math.Clamp(index, 0, Math.Max(0, owner.items.Count - 1));
            scroll.Value = ResponsiveGeometry.Reveal(scroll.Value, ContentBounds.Height, HighlightedIndex * owner.EffectiveItemHeight,
                owner.EffectiveItemHeight, owner.items.Count * owner.EffectiveItemHeight);
            Invalidate();
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            UiDrawing.ControlBackground(e.Graphics, ClientRectangle, UiPalette.Card);
            Rectangle area = ContentBounds;
            var saved = e.Graphics.Save();
            try
            {
                e.Graphics.SetClip(area, CombineMode.Intersect);
                int rowHeight = owner.EffectiveItemHeight, first = scroll.Value / rowHeight;
                for (int index = first; index < owner.items.Count; index++)
                {
                    Rectangle row = new(area.Left, area.Top + index * rowHeight - scroll.Value, area.Width, rowHeight);
                    if (row.Top >= area.Bottom) break;
                    bool selected = index == owner.selectedIndex, highlighted = index == HighlightedIndex;
                    if (selected || highlighted) UiDrawing.FillRound(e.Graphics, Rectangle.Inflate(row, 0, -1), LogicalToDeviceUnits(5), highlighted ? UiPalette.Hover : UiPalette.Selection);
                    int inset = LogicalToDeviceUnits(10), checkWidth = LogicalToDeviceUnits(24);
                    Rectangle label = new(row.Left + inset, row.Top, Math.Max(1, row.Width - 2 * inset - checkWidth), row.Height);
                    TextRenderer.DrawText(e.Graphics, owner.GetItemText(owner.items[index]), Font, label, UiPalette.Text,
                        UiDrawing.ButtonTextFlags(ContentAlignment.MiddleLeft, false));
                    if (selected) UiIcons.Draw(e.Graphics, new Rectangle(row.Right - checkWidth, row.Top + (row.Height - LogicalToDeviceUnits(16)) / 2,
                        LogicalToDeviceUnits(16), LogicalToDeviceUnits(16)), UiIcon.Check, UiPalette.Text);
                }
            }
            finally { e.Graphics.Restore(saved); }
        }
        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (!ContentBounds.Contains(e.Location)) return;
            int row = DropDownGeometry.HitRow(e.Y - ContentBounds.Top, scroll.Value, owner.EffectiveItemHeight, owner.items.Count);
            if (row >= 0 && HighlightedIndex != row) { HighlightedIndex = row; Invalidate(); }
        }
        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button != MouseButtons.Left || !ContentBounds.Contains(e.Location)) return;
            int row = DropDownGeometry.HitRow(e.Y - ContentBounds.Top, scroll.Value, owner.EffectiveItemHeight, owner.items.Count);
            if (row >= 0) owner.Commit(row);
        }
        protected override bool IsInputKey(Keys keyData) => (keyData & Keys.KeyCode) is Keys.Up or Keys.Down or Keys.Home or Keys.End or Keys.PageUp or Keys.PageDown || base.IsInputKey(keyData);
        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            Keys key = keyData & Keys.KeyCode;
            if (key is Keys.Escape or Keys.F4 || (keyData & Keys.Alt) != 0 && key is Keys.Up or Keys.Down)
            { owner.CloseDropDown(); owner.Focus(); return true; }
            if (key == Keys.Enter) { owner.Commit(HighlightedIndex); return true; }
            if (key == Keys.Tab)
            {
                owner.CloseDropDown(); owner.Focus();
                owner.FindForm()?.SelectNextControl(owner, (keyData & Keys.Shift) == 0, true, true, true);
                return true;
            }
            return owner.HandleNavigation(key, commit: false) || base.ProcessCmdKey(ref msg, keyData);
        }
        protected override void OnKeyPress(KeyPressEventArgs e) { if (!char.IsControl(e.KeyChar)) { owner.TypeSearch(e.KeyChar, true); e.Handled = true; } base.OnKeyPress(e); }
        bool IScrollWheelTarget.ScrollWheel(WheelInput input) { if (!input.Horizontal) scroll.ScrollOwn(input); return true; }
        protected override void OnMouseWheel(MouseEventArgs e) { ((IScrollWheelTarget)this).ScrollWheel(WheelInput.From(e)); if (e is HandledMouseEventArgs handled) handled.Handled = true; }
        private sealed class ListAccessibleObject(DropDownList view) : ControlAccessibleObject(view)
        {
            public override AccessibleRole Role => AccessibleRole.List;
            public override int GetChildCount() => view.owner.items.Count;
            public override AccessibleObject? GetChild(int index) => index >= 0 && index < view.owner.items.Count ? new RowAccessibleObject(view, index) : null;
            public override AccessibleObject? GetSelected() => GetChild(view.owner.selectedIndex);
            public override AccessibleObject? GetFocused() => view.ContainsFocus ? GetChild(view.HighlightedIndex) : null;
        }
        private sealed class RowAccessibleObject(DropDownList view, int index) : AccessibleObject
        {
            public override AccessibleRole Role => AccessibleRole.ListItem;
            public override AccessibleObject? Parent => view.AccessibilityObject;
            public override string? Name { get => index < view.owner.items.Count ? view.owner.GetItemText(view.owner.items[index]) : ""; set { } }
            public override string? DefaultAction => "Select";
            public override AccessibleStates State => AccessibleStates.Selectable | AccessibleStates.Focusable
                | (index == view.owner.selectedIndex ? AccessibleStates.Selected : 0)
                | (view.ContainsFocus && index == view.HighlightedIndex ? AccessibleStates.Focused : 0)
                | (Bounds.IsEmpty ? AccessibleStates.Offscreen : 0);
            public override Rectangle Bounds
            {
                get
                {
                    if (!view.Visible || view.IsDisposed) return Rectangle.Empty;
                    Rectangle content = view.ContentBounds;
                    Rectangle row = new(content.Left, content.Top + index * view.owner.EffectiveItemHeight - view.scroll.Value, content.Width, view.owner.EffectiveItemHeight);
                    row.Intersect(content);
                    return row.IsEmpty ? Rectangle.Empty : view.RectangleToScreen(row);
                }
            }
            public override void DoDefaultAction() { if (!view.IsDisposed && index < view.owner.items.Count) view.owner.Commit(index); }
        }
    }
}
