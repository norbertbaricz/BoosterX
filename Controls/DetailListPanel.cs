namespace BoosterX.App.Controls;

/// <summary>Full-width actions and wrapped details. The page owns scrolling; rows never host a scrollbar.</summary>
internal sealed class DetailListPanel : BufferedPanel
{
    private sealed class Entry(Control action,Label description,string category)
    {
        internal Control Action { get; } = action;
        internal Label Description { get; } = description;
        internal string Category { get; } = category;
        internal string Search { get; set; } = action.Text+" "+description.Text+" "+category;
    }
    private readonly record struct Placement(Control Control,Rectangle Bounds);
    private sealed record Measurement(int Width,int Dpi,int Height,Placement[] Placements);
    private readonly List<Entry> entries = [];
    private readonly Dictionary<string,Label> headings = new(StringComparer.Ordinal);
    private string query = "";
    private bool arranging;
    private Font? headingFont;
    private Measurement? measured;
    private int measurementCount;

    internal int EntryCount => entries.Count;
    internal int ShownCount => entries.Count(Matches);
    internal int MeasurementCount => measurementCount;

    internal DetailListPanel()
    {
        AutoSize = true; AutoSizeMode = AutoSizeMode.GrowAndShrink;
        Dock = DockStyle.Top; Margin = Padding.Empty;
    }
    internal void Add(Control action,string description,string category)
    {
        SuspendLayout();
        try
        {
            action.AutoSize = false; action.Dock = DockStyle.None; action.Anchor = AnchorStyles.Top | AnchorStyles.Left;
            action.Margin = Padding.Empty;
            var detail = new Label { Name = action.Name+"Details",Text = description,UseMnemonic = false,Tag = "muted",
                AutoSize = false,Margin = Padding.Empty,AccessibleName = action.Text+" details" };
            action.AccessibleDescription = description;
            if (!headings.ContainsKey(category))
            {
                headingFont ??= new Font(Font,FontStyle.Bold);
                var heading = new Label { Name = Name+"Section"+headings.Count,Text = category,UseMnemonic = false,Font = headingFont,
                    AutoSize = false,Margin = Padding.Empty };
                headings.Add(category,heading); Controls.Add(heading);
            }
            var entry = new Entry(action,detail,category);
            entries.Add(entry);
            void ContentChanged(object? sender,EventArgs e)
            {
                entry.Search = action.Text+" "+detail.Text+" "+category;
                measured = null;
            }
            action.TextChanged += ContentChanged; action.FontChanged += ContentChanged; action.PaddingChanged += ContentChanged;
            detail.TextChanged += ContentChanged; detail.FontChanged += ContentChanged;
            measured = null; Controls.Add(action); Controls.Add(detail);
        }
        finally { ResumeLayout(false); }
    }
    internal void Filter(string text)
    {
        string next = text.Trim();
        if (query == next) return;
        query = next; measured = null;
        SuspendLayout();
        try
        {
            foreach (Entry entry in entries) entry.Action.Visible = entry.Description.Visible = Matches(entry);
            foreach ((string category,Label heading) in headings) heading.Visible = entries.Any(e => e.Category == category && Matches(e));
        }
        finally { ResumeLayout(true); }
        Parent?.PerformLayout();
    }
    private bool Matches(Entry entry) => query.Length == 0 || entry.Search.Contains(query,StringComparison.OrdinalIgnoreCase);
    public override Size GetPreferredSize(Size proposedSize)
    {
        if (entries == null) return base.GetPreferredSize(proposedSize);
        int width = Math.Max(1,proposedSize.Width > 0 ? proposedSize.Width : Width);
        return new(width,MeasureRows(width).Height);
    }
    protected override void OnLayout(LayoutEventArgs e)
    {
        if (arranging || entries == null) return;
        if (e.AffectedControl != this && e.AffectedProperty is "Font" or "Text" or "Padding" or "MinimumSize" or "Symbol") measured = null;
        arranging = true;
        try
        {
            base.OnLayout(e);
            foreach (Placement placement in MeasureRows(Math.Max(1,ClientSize.Width)).Placements)
                if (placement.Control.Bounds != placement.Bounds) placement.Control.Bounds = placement.Bounds;
        }
        finally { arranging = false; }
    }
    private Measurement MeasureRows(int width)
    {
        if (measured is { } previous && previous.Width == width && previous.Dpi == DeviceDpi) return previous;
        int y = 0, gap = LogicalToDeviceUnits(UiPalette.SectionGap);
        var placements = new List<Placement>();
        Entry[] shown = entries.Where(Matches).ToArray();
        int actionWidth = shown.Length == 0 ? 1 : shown.Max(e => e.Action.GetPreferredSize(Size.Empty).Width);
        foreach ((string category,Label heading) in headings)
        {
            Entry[] group = shown.Where(e => e.Category == category).ToArray();
            if (group.Length == 0) continue;
            if (y > 0) y += LogicalToDeviceUnits(UiPalette.DetailSectionGap);
            int headingHeight = MeasureText(heading,width);
            placements.Add(new(heading,new(0,y,width,headingHeight)));
            y += headingHeight+gap;
            foreach (Entry entry in group)
            {
                int actionHeight = Math.Max(LogicalToDeviceUnits(UiPalette.FieldHeight),entry.Action.GetPreferredSize(Size.Empty).Height);
                bool stacked = DetailListGeometry.Stacked(width,actionWidth,DeviceDpi);
                int detailWidth = stacked ? width : Math.Max(1,width-actionWidth-gap);
                int detailHeight = MeasureText(entry.Description,detailWidth);
                DetailRowBounds row = DetailListGeometry.Arrange(width,actionWidth,actionHeight,detailHeight,DeviceDpi);
                Rectangle action = row.Action, description = row.Description;
                action.Offset(0,y); description.Offset(0,y);
                placements.Add(new(entry.Action,action)); placements.Add(new(entry.Description,description));
                y += row.Height+gap;
            }
        }
        measurementCount++;
        return measured = new(width,DeviceDpi,Math.Max(0,y-gap),placements.ToArray());
    }
    private int MeasureText(Label label,int width) => TextRenderer.MeasureText(label.Text,label.Font,new(Math.Max(1,width),int.MaxValue),
        TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix | TextFormatFlags.TextBoxControl).Height+LogicalToDeviceUnits(2);

    internal IEnumerable<string> VerifyRows()
    {
        foreach (Entry entry in entries.Where(Matches))
        {
            if (!ClientRectangle.Contains(entry.Action.Bounds) || !ClientRectangle.Contains(entry.Description.Bounds))
                yield return entry.Action.Name+" extends beyond its action list.";
            if (entry.Action.Bounds.IntersectsWith(entry.Description.Bounds)) yield return entry.Action.Name+" overlaps its description.";
            if (entry.Description.Height < MeasureText(entry.Description,entry.Description.Width)) yield return entry.Action.Name+" clips its wrapped description.";
            if (entry.Action.GetPreferredSize(Size.Empty).Width > entry.Action.Width) yield return entry.Action.Name+" clips its title.";
        }
    }
    protected override void OnFontChanged(EventArgs e)
    {
        measured = null;
        base.OnFontChanged(e);
        if (headings == null) return;
        Font? previous = headingFont; headingFont = new Font(Font,FontStyle.Bold);
        foreach (Label heading in headings.Values) heading.Font = headingFont;
        previous?.Dispose(); measured = null; PerformLayout();
    }
    protected override void OnDpiChangedAfterParent(EventArgs e)
    {
        measured = null; base.OnDpiChangedAfterParent(e); PerformLayout();
    }
    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing) { measured = null; headingFont?.Dispose(); }
    }
}
