using System.Runtime.InteropServices;
using BoosterX.App.Controls;

namespace BoosterX.App.Services;

/// <summary>Windows-only fixtures using the real dropdowns, scroll hosts and keyboard/mouse messages.</summary>
internal static class InteractionChecks
{
    internal static IReadOnlyList<RenderingCheckResult> Run(Font font, string? directory = null)
    {
        var results = new List<RenderingCheckResult>();
        var dispatchedExceptions = new List<Exception>();
        System.Threading.ThreadExceptionEventHandler capture = (_,args) => dispatchedExceptions.Add(args.Exception);
        Application.ThreadException += capture;
        void Check(string name, Action action)
        {
            int before = dispatchedExceptions.Count;
            try
            {
                action(); Pump();
                if (dispatchedExceptions.Count > before) throw new InvalidOperationException("Dispatched UI exception: " + dispatchedExceptions[before]);
                results.Add(new(name, true, "Passed"));
            }
            catch (Exception ex) { results.Add(new(name, false, ex.Message)); }
        }
        Check("Custom dropdown preserves typed selection and change events", () =>
        {
            using var selector = new DarkComboBox();
            var alpha = new Choice("Alpha"); var beta = new Choice("Beta");
            selector.DataSource = new[] { alpha, beta };
            int changed = 0; selector.SelectedIndexChanged += (_, _) => changed++;
            selector.SelectedIndex = 1; selector.SelectedIndex = 1;
            Require(ReferenceEquals(selector.SelectedItem, beta) && changed == 1, "Typed selection or event semantics changed.");
            selector.SelectedIndex = -1;
            Require(selector.SelectedItem == null, "A stale item survived clearing the selection.");
            selector.DataSource = null; selector.Items.AddRange(["All events", "Success", "Warnings", "Errors"]);
            selector.SelectedIndex = 0; selector.HandleNavigation(Keys.End, true);
            Require(selector.SelectedIndex == 3 && Equals(selector.SelectedItem, "Errors"), "Activity-filter navigation selected the wrong item.");
        });
        Check("Dropdown closed surface clears opaque corners and retains disabled text", () =>
        {
            using var form = Fixture(font);
            var selector = new DarkComboBox { Size = new(300, 40), Symbol = UiIcon.Network, AccessibleName = "Adapter" };
            selector.Items.Add("Example adapter"); selector.SelectedIndex = 0; form.Controls.Add(selector); Show(form);
            foreach (bool enabled in new[] { true, false, true })
            {
                selector.Enabled = enabled;
                using var bitmap = new Bitmap(selector.Width, selector.Height);
                selector.DrawToBitmap(bitmap, new(Point.Empty, bitmap.Size));
                Require(bitmap.GetPixel(0, 0).ToArgb() == form.BackColor.ToArgb(), "Dropdown left stale corner pixels.");
                Require(selector.AccessibilityObject.Role == AccessibleRole.ComboBox, "Dropdown lost its accessible role.");
            }
        });
        Check("Dropdown Escape cancels preview without changing the selected value", () =>
        {
            using var form = Fixture(font); var selector = Selector(); form.Controls.Add(selector); Show(form);
            Key(selector, Keys.F4); Require(selector.DroppedDown, "F4 did not open the popup.");
            Key(selector.PopupList!, Keys.Down);
            Require(selector.SelectedIndex == 0 && selector.PopupList!.HighlightedIndex == 1, "Preview changed the committed selection.");
            Key(selector.PopupList!, Keys.Escape);
            Require(!selector.DroppedDown && selector.SelectedIndex == 0, "Escape did not cancel the preview.");
        });
        Check("Dropdown End and Enter reach and commit its last option", () =>
        {
            using var form = Fixture(font); var selector = Selector(); form.Controls.Add(selector); Show(form);
            selector.OpenDropDown(); Pump();
            var list = selector.PopupList!; Key(list, Keys.End);
            Require(list.HighlightedIndex == selector.Items.Count - 1 && list.ScrollOffset > 0, "Keyboard navigation could not reach the final option.");
            Require(Screen.FromControl(selector).WorkingArea.Contains(list.RectangleToScreen(list.ClientRectangle)), "Popup escaped the monitor work area.");
            Require(list.AccessibilityObject.GetChildCount() == selector.Items.Count, "Popup options are missing from accessibility.");
            if (directory != null)
            {
                Directory.CreateDirectory(directory); using var bitmap = new Bitmap(list.Width, list.Height);
                list.DrawToBitmap(bitmap, new(Point.Empty, bitmap.Size)); bitmap.Save(Path.Combine(directory, "dropdown-options.png"));
            }
            Key(list, Keys.Enter);
            Require(!selector.DroppedDown && selector.SelectedIndex == 49, "Enter did not commit and close the popup.");
        });
        Check("Wheel over a page button accumulates fine input without moving focus", () =>
        {
            using var form = Fixture(font); var page = Page(new(240, 180));
            var content = new BufferedPanel { MinimumSize = new(0, 1000), Size = new(220, 1000) };
            var button = new RoundedButton { Text = "Action", Size = new(130, 40), Location = new(8, 8) };
            var focus = new TextBox { Location = new(300, 20) };
            content.Controls.Add(button); page.Viewport.Controls.Add(content); form.Controls.Add(page); form.Controls.Add(focus); Show(form); focus.Focus();
            var bar = Vertical(page);
            for (int i = 0; i < 3; i++) Require(ScrollWheelRouter.Dispatch(button, new(-40, false, 3)), "Fine wheel input was not consumed.");
            Require(bar.Value > 0 && focus.Focused, "Button hover did not scroll the page independently of focus.");
        });
        Check("Report wheel scrolling moves native text and preserves selection", () =>
        {
            using var form = Fixture(font);
            var text = new DarkTextView { Size = new(300, 160), Text = string.Join('\n', Enumerable.Range(0, 200).Select(i => "Report line " + i)), WordWrap = true };
            form.Controls.Add(text); Show(form); var bar = Vertical(text); bar.Value = 0; Pump();
            text.Select(0, 6); int start = text.SelectionStart, length = text.Editor.SelectionLength;
            Require(ScrollWheelRouter.Dispatch(text.Editor, new(-120, false, 3)), "Report did not accept the wheel."); Pump();
            Require(bar.Value > 0 && FirstTextLine(text) > 0, "Native report text stayed at its original position.");
            Require(text.SelectionStart == start && text.Editor.SelectionLength == length, "Scrolling changed the text selection.");
        });
        Check("Nested report edges hand remaining wheel input to their page", () =>
        {
            using var form = Fixture(font); var page = Page(new(340, 230));
            var content = new BufferedPanel { MinimumSize = new(0, 900), Size = new(320, 900) };
            var text = new DarkTextView { Size = new(280, 120), Location = new(8, 8), Text = string.Join('\n', Enumerable.Range(0, 100).Select(i => "Line " + i)) };
            content.Controls.Add(text); page.Viewport.Controls.Add(content); form.Controls.Add(page); Show(form);
            Vertical(text).Value = Vertical(text).Maximum; Pump(); int before = Vertical(page).Value;
            Require(ScrollWheelRouter.Dispatch(text.Editor, new(-120, false, 3)), "Nested edge input was lost.");
            Require(Vertical(page).Value > before, "Parent page did not receive input at the report's bottom edge.");
        });
        Check("Grid bars reach final variable-height rows and the rightmost column", () =>
        {
            using var form = Fixture(font); var host = Grid(out DarkDataGridView grid); form.Controls.Add(host); Show(form);
            var vertical = Vertical(host); var horizontal = Horizontal(host);
            Require(vertical.Visible && horizontal.Visible, "Required grid scrollbars are missing.");
            vertical.Value = vertical.Maximum; horizontal.Value = horizontal.Maximum; Pump();
            Require(grid.FirstDisplayedScrollingRowIndex >= 2 && grid.HorizontalScrollingOffset > 0, "Grid did not scroll beyond hidden/frozen rows or columns.");
            Rectangle last = grid.GetRowDisplayRectangle(grid.Rows.Count - 1, false);
            Require(last.Height > 0 && last.Bottom <= grid.ClientSize.Height, "The final grid row is unreachable.");
            Require(grid.GetColumnDisplayRectangle(1, false).Right <= grid.ClientSize.Width, "The rightmost column is clipped at maximum offset.");
        });
        Check("Grid bars disappear after data and column extents shrink", () =>
        {
            using var form = Fixture(font); var host = Grid(out DarkDataGridView grid); form.Controls.Add(host); Show(form);
            Require(Vertical(host).Visible && Horizontal(host).Visible, "Initial overflow fixture did not overflow.");
            grid.Rows.Clear(); grid.Columns[1].Width = 40; Pump();
            Require(!Vertical(host).Visible && !Horizontal(host).Visible, "Stale scrollbars retained each other after the content shrank.");
        });
        Check("Custom thumb drag updates the real scroll value", () =>
        {
            using var form = Fixture(font); var bar = new DarkScrollBar { Size = new(10, 180) }; form.Controls.Add(bar); Show(form);
            bar.Configure(180, 1800, 0);
            SendMessage(bar.Handle, 0x0201, 1, PackPoint(5, 8));
            SendMessage(bar.Handle, 0x0200, 1, PackPoint(5, 160));
            SendMessage(bar.Handle, 0x0202, 0, PackPoint(5, 160));
            Require(bar.Value > 0 && bar.Value <= bar.Maximum, "Thumb dragging did not move the scroll value.");
        });
        Check("Selectors reopen and commit repeatedly without disposing their popup", () =>
        {
            using var form = Fixture(font); var selector = Selector(); form.Controls.Add(selector); Show(form);
            for (int i = 0; i < 100; i++)
            {
                selector.OpenDropDown(); Pump(); Require(selector.DroppedDown,"Popup did not reopen at cycle " + i);
                Key(selector.PopupList!,i % 2 == 0 ? Keys.End : Keys.Home); Key(selector.PopupList!,Keys.Enter);
                Require(!selector.DroppedDown && selector.SelectedIndex == (i % 2 == 0 ? 49 : 0),"Selection/close failed at cycle " + i);
            }
        });
        Check("A selector survives a bound-source replacement between openings", () =>
        {
            using var form = Fixture(font); var selector = Selector(); form.Controls.Add(selector); Show(form);
            for (int i = 0; i < 10; i++)
            {
                selector.DataSource = new[] { new Choice("Adapter " + i),new Choice("Adapter alternate " + i) };
                selector.OpenDropDown(); Pump(); Key(selector.PopupList!,Keys.End); Key(selector.PopupList!,Keys.Enter);
                Require(selector.SelectedItem is Choice choice && choice.Label == "Adapter alternate " + i,"Binding lost the selected adapter object.");
            }
        });
        Check("Search fields and selectors share the same aligned field height", () =>
        {
            using var form = Fixture(font);
            var search = new DarkSearchBox { Size = new(230,40),PlaceholderText = "Search…" };
            var selector = new DarkComboBox { Location = new(250,0),Size = new(200,40) };
            form.Controls.Add(search); form.Controls.Add(selector); Show(form);
            Require(search.Height == selector.Height && search.Top == selector.Top,"Field baselines/heights differ.");
            TextBox editor = search.Controls.OfType<TextBox>().Single();
            Require(Math.Abs(editor.Top-(search.Height-editor.Height)/2) <= 1,"Search text is not vertically centered.");
        });
        Check("Maintenance buttons and full descriptions fit wide and stacked rows", () =>
        {
            using var form = Fixture(font);
            var list = new DetailListPanel { Name = "maintenanceFixture",AutoSize = false,Dock = DockStyle.None };
            list.Add(new RoundedButton { Text = "Optimize system drive using Windows",Symbol = UiIcon.Disk },
                string.Join(' ',Enumerable.Repeat("Wrapped maintenance details remain readable.",12)),"Storage");
            list.Add(new RoundedButton { Text = "Inspect IP configuration",Symbol = UiIcon.Network },"Read only.","Network");
            form.Controls.Add(list); Show(form);
            foreach (int logicalWidth in new[] { 520,760,1000,520 })
            {
                int width = list.LogicalToDeviceUnits(logicalWidth);
                list.Size = list.GetPreferredSize(new(width,0)); list.PerformLayout(); Pump();
                Require(!list.VerifyRows().Any(),string.Join("; ",list.VerifyRows()));
            }
        });
        Check("Maintenance action search hides entire rows and restores all actions", () =>
        {
            using var form = Fixture(font); var list = new DetailListPanel();
            list.Add(new RoundedButton { Text = "Inspect IP configuration",Symbol = UiIcon.Search },"Read IP settings.","Network");
            list.Add(new RoundedButton { Text = "Verify protected files",Symbol = UiIcon.Search },"Read system files.","Windows");
            form.Controls.Add(list); Show(form);
            list.Filter("Network"); Require(list.ShownCount == 1,"Category search is incorrect.");
            list.Filter("missing-fixture"); Require(list.ShownCount == 0,"Unrelated rows remain visible.");
            list.Filter(""); Require(list.ShownCount == 2,"Clearing search lost an action.");
        });
        Check("Natural maintenance pages discard stale height after resize and search", () =>
        {
            using var form = Fixture(font);
            var page = new DarkScrollPanel { Size = new(600,240),LogicalMinimumContentWidth = 520 };
            var table = new BufferedTableLayoutPanel { ColumnCount = 1,RowCount = 2,Size = new(600,8000),Margin = Padding.Empty };
            table.ColumnStyles.Add(new(SizeType.Percent,100));
            table.RowStyles.Add(new(SizeType.AutoSize)); table.RowStyles.Add(new(SizeType.AutoSize));
            table.Controls.Add(new Label { AutoSize = true,Dock = DockStyle.Fill,Text = "Maintenance fixture",Margin = Padding.Empty },0,0);
            var list = new DetailListPanel();
            for (int i = 0; i < 3; i++) list.Add(new RoundedButton { Text = "Action "+i,Symbol = UiIcon.Wrench },
                string.Join(' ',Enumerable.Repeat("Complete maintenance description.",6)),"Windows");
            table.Controls.Add(list,0,1); page.Viewport.Controls.Add(table); form.Controls.Add(page); Show(form);
            void VerifyExtent()
            {
                Pump();
                int expected = Math.Max(page.Viewport.Height,table.GetPreferredSize(new(table.Width,0)).Height);
                Require(table.Height <= expected+1,"Old page height remains in the scroll extent.");
                if (table.Height > page.Viewport.Height)
                {
                    int bottom = list.Top+list.GetPreferredSize(new(list.Width,0)).Height;
                    Require(table.Height-bottom <= page.LogicalToDeviceUnits(UiPalette.SectionGap),"Blank trailing page space remains scrollable.");
                }
            }
            VerifyExtent(); page.Size = new(1300,500); VerifyExtent();
            list.Filter("Action 2"); VerifyExtent();
            Require(!Vertical(page).Visible,"A filtered, fitting repair page still has a vertical scrollbar.");
            list.Filter("missing-action-fixture"); VerifyExtent();
            Require(!Vertical(page).Visible,"An empty search retains the old scrollbar.");
            list.Filter(""); page.Size = new(520,240); VerifyExtent();
        });
        Check("Maintenance size queries reuse measured text until content changes", () =>
        {
            using var form = Fixture(font); var list = new DetailListPanel();
            list.Add(new RoundedButton { Text = "Inspect IP configuration",Symbol = UiIcon.Search },"Read-only details that wrap when space is narrow.","Network");
            form.Controls.Add(list); Show(form);
            int width = list.Width; list.GetPreferredSize(new(width,0)); int count = list.MeasurementCount;
            for (int i = 0; i < 100; i++) list.GetPreferredSize(new(width,i*100));
            Require(list.MeasurementCount == count,"Unchanged layout remeasures and reallocates every row.");
            list.Filter("missing-action-fixture");
            Require(list.GetPreferredSize(new(width,0)).Height == 0,"Empty results retain old text height.");
            list.Filter(""); Require(list.GetPreferredSize(new(width,0)).Height > 0,"Clearing search lost the original layout.");
        });
        Check("Cleanup preview percent rows fit and recover after a narrow resize", () =>
        {
            using var form = Fixture(font);
            var page = new DarkScrollPanel { Size = new(600,420),LogicalMinimumContentWidth = 520 };
            var table = new BufferedTableLayoutPanel { ColumnCount = 1,RowCount = 3,Size = new(600,8000),Margin = Padding.Empty };
            table.ColumnStyles.Add(new(SizeType.Percent,100));
            table.RowStyles.Add(new(SizeType.AutoSize)); table.RowStyles.Add(new(SizeType.AutoSize)); table.RowStyles.Add(new(SizeType.Percent,100));
            table.Controls.Add(new Label { AutoSize = true,Dock = DockStyle.Fill,Margin = Padding.Empty,Text = "Scan all 22 cleanup areas automatically." },0,0);
            table.Controls.Add(new RoundedButton { AutoSize = true,Text = "Scan cleanup areas",Symbol = UiIcon.Search,Margin = Padding.Empty },0,1);
            table.Controls.Add(new BufferedPanel { Dock = DockStyle.Fill,MinimumSize = new(0,120),Margin = Padding.Empty },0,2);
            page.Viewport.Controls.Add(table); form.Controls.Add(page); Show(form);
            Require(!Vertical(page).Visible && table.Height == page.Viewport.Height,"Compact cleanup retains a large empty scroll extent.");
            page.Size = new(600,80); Pump(); Require(Vertical(page).Visible,"Small cleanup cannot reach the required preview height.");
            page.Size = new(600,420); Pump(); Require(!Vertical(page).Visible,"Cleanup retains a scrollbar after growing again.");
        });
        Application.ThreadException -= capture;
        return results;
    }
    private static Form Fixture(Font font) => new() { ShowInTaskbar = false, StartPosition = FormStartPosition.Manual,
        Location = new(-30000, -30000), ClientSize = new(700, 500), Font = font, BackColor = UiPalette.Background };
    private static DarkComboBox Selector()
    {
        var selector = new DarkComboBox { Location = new(12, 12), Size = new(260, 40) };
        selector.Items.AddRange(Enumerable.Range(0, 50).Select(i => (object)("Option " + i.ToString("00"))).ToArray());
        selector.SelectedIndex = 0; return selector;
    }
    private static DarkScrollPanel Page(Size size) => new() { Size = size };
    private static DarkGridHost Grid(out DarkDataGridView grid)
    {
        var host = new DarkGridHost { Size = new(260, 180) };
        grid = new DarkDataGridView { AllowUserToAddRows = false, RowHeadersVisible = true, RowHeadersWidth = 40,
            AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.None, BorderStyle = BorderStyle.None };
        grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Frozen", Width = 70, Frozen = true });
        grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Wide", Width = 600 });
        for (int i = 0; i < 40; i++) { int index = grid.Rows.Add("Row " + i, "Contents"); grid.Rows[index].Height = i % 3 == 0 ? 70 : 30; }
        grid.Rows[0].Frozen = true; grid.Rows[1].Visible = false;
        host.Viewport.Controls.Add(grid); return host;
    }
    private static DarkScrollBar Vertical(Control owner) => owner.Controls.OfType<DarkScrollBar>().Single(bar => bar.Orientation == Orientation.Vertical);
    private static DarkScrollBar Horizontal(Control owner) => owner.Controls.OfType<DarkScrollBar>().Single(bar => bar.Orientation == Orientation.Horizontal);
    private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    private static void Show(Form form) { form.Show(); form.PerformLayout(); Pump(); }
    private static void Pump() { for (int i = 0; i < 4; i++) Application.DoEvents(); }
    private static void Key(Control control, Keys key)
    {
        Message message = Message.Create(control.Handle, 0x0100, (nint)key, 0);
        if (!control.PreProcessMessage(ref message)) SendMessage(control.Handle, message.Msg, message.WParam, message.LParam);
        Pump();
    }
    private static int FirstTextLine(DarkTextView text) => (int)SendMessage(text.Editor.Handle, 0x00CE, 0, 0);
    private static nint PackPoint(int x, int y) => (nint)((y << 16) | (x & 0xffff));
    private sealed record Choice(string Label) { public override string ToString() => Label; }
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern nint SendMessage(nint window, int message, nint wParam, nint lParam);
}
