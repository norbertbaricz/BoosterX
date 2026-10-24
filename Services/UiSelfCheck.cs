using System.Text.Json;
using System.Runtime.InteropServices;
using BoosterX.Core;
using BoosterX.App.Controls;

namespace BoosterX.App.Services;

/// <summary>Runs on Windows against the real WinForms layout, using fixture data and no optimization operations.</summary>
internal static class UiSelfCheck
{
    internal static int Run(string dataDirectory)
    {
        int exitCode = 1;
        var failures = new List<string>();
        System.Threading.ThreadExceptionEventHandler capture = (_,args) => failures.Add("Dispatched UI exception: " + args.Exception);
        Application.ThreadException += capture;
        using var form = new Form1(layoutCheckOnly: true);
        form.Shown += async (_, _) =>
        {
            var results = new List<object>();
            try
            {
                string directory = Path.Combine(dataDirectory, "UiCheck");
                Directory.CreateDirectory(directory);
                form.PrepareLayoutCheck();
                failures.AddRange(form.VerifyFeatureFixtures());
                failures.AddRange(ControlRenderingChecks.Run(form.Font, directory).Where(r => !r.Passed).Select(r => r.Name + ": " + r.Details));
                failures.AddRange(InteractionChecks.Run(form.Font, directory).Where(r => !r.Passed).Select(r => r.Name + ": " + r.Details));
                failures.AddRange(CheckBorderlessButtons(form.Font));
                failures.AddRange(CheckPartialPaints(form.Font));
                foreach (Size logical in new[] { new Size(1280, 720), new Size(1100, 720), new Size(1099, 720),
                    new Size(1080, 720), new Size(1079, 720), new Size(1024, 720), new Size(960, 640),
                    new Size(900, 640), new Size(800, 600), new Size(760, 600), new Size(759, 600),
                    new Size(640, 480), new Size(480, 320) })
                {
                    form.SetLayoutCheckSize(logical);
                    foreach (string page in form.LayoutPageNames)
                    {
                        foreach (string query in page == "repairs" ? new[] { "","Winsock","missing-action-fixture" } : new[] { "" })
                        {
                            form.SelectLayoutPage(page);
                            form.SetRepairLayoutCheck(query);
                            form.PerformLayout();
                            await Task.Delay(UiPalette.UiCheckSettleMilliseconds); // Yield to queued viewport layout and the native message loop.
                            string[] issues = FindLayoutIssues(form).Concat(FindHeaderIssues(form)).Concat(FindVisualIssues(form)).Concat(form.VerifyPageAlignment()).ToArray();
                            failures.AddRange(issues.Select(issue => $"{page} / {form.ClientSize}: {issue}"));
                            string snapshot = $"{results.Count + 1:00}-{page}{(query.Length > 0 ? "-filter"+results.Count : "")}-{form.ClientSize.Width}x{form.ClientSize.Height}.png";
                            using (var bitmap = new Bitmap(Math.Max(1, form.Width), Math.Max(1, form.Height)))
                            {
                                // Form/control-only output; never capture the user's desktop or other windows.
                                // Native RichEdit text is not reliably included by DrawToBitmap.
                                form.DrawToBitmap(bitmap, new Rectangle(Point.Empty, bitmap.Size));
                                bitmap.Save(Path.Combine(directory, snapshot), System.Drawing.Imaging.ImageFormat.Png);
                            }
                            results.Add(new { Page = page,RepairSearch = query, Width = form.ClientSize.Width, Height = form.ClientSize.Height,
                                Dpi = form.DeviceDpi, Snapshot = snapshot, Issues = issues });
                        }
                        form.SetRepairLayoutCheck("");
                    }
                }
                exitCode = failures.Count == 0 ? 0 : 1;
            }
            catch (Exception ex) { failures.Add(ex.ToString()); }
            finally
            {
                try
                {
                    string directory = Path.Combine(dataDirectory, "UiCheck");
                    Directory.CreateDirectory(directory);
                    File.WriteAllText(Path.Combine(directory, "latest.json"), JsonSerializer.Serialize(new
                    {
                        Product = BoosterOptions.ProductVersion, Framework = "Windows Forms", DateUtc = DateTimeOffset.UtcNow,
                        Cases = results, Failures = failures, OptimizationsApplied = false,
                        SnapshotMethod = "WinForms DrawToBitmap; native RichEdit text may be omitted"
                    }, JsonDefaults.Options));
                }
                catch (Exception) { exitCode = 1; }
                finally { form.Close(); }
            }
        };
        try { Application.Run(form); }
        finally { Application.ThreadException -= capture; }
        return exitCode;
    }

    private static IEnumerable<string> FindHeaderIssues(Form form)
    {
        Control header = form.Controls.Find("headerLayout", true).Single();
        foreach (RoundedButton button in Theme.Descendants(header).OfType<RoundedButton>().Where(b => b.Visible))
        {
            Rectangle screenBounds = button.RectangleToScreen(button.ClientRectangle);
            // Checking just the immediate FlowLayoutPanel missed the screenshot's clipped second row.
            for (Control? ancestor = button.Parent; ancestor != null; ancestor = ancestor.Parent)
            {
                if (!ancestor.RectangleToScreen(ancestor.ClientRectangle).Contains(screenBounds))
                {
                    yield return button.Name + " is clipped by header ancestor " + ancestor.Name + ".";
                    break;
                }
                if (ancestor == form) break;
            }
        }
        var tools = (FlowLayoutPanel)form.Controls.Find("headerToolsFlow", true).Single();
        if (tools.WrapContents) yield return "Header action buttons can still wrap to a clipped row.";
        var visibleActions = tools.Controls.OfType<RoundedButton>().Where(b => b.Visible).ToArray();
        if (visibleActions.Select(b => b.Top - b.Margin.Top).Distinct().Count() > 1)
            yield return "Header action buttons no longer share a single row.";
    }

    private static IEnumerable<string> FindLayoutIssues(Form form)
    {
        foreach (Control container in Theme.Descendants(form).Prepend(form))
        {
            if (!container.Visible) continue;
            var children = container.Controls.Cast<Control>().Where(c => c.Visible).ToArray();
            foreach (Control child in children)
            {
                if (child is not (Button or CheckBox or Label or DarkComboBox)) continue;
                if (child.Width <= 0 || child.Height <= 0) { yield return child.Name + " has no visible area."; continue; }
                bool scrolls = container is ScrollableControl { AutoScroll: true };
                if (!scrolls && (child.Left < -2 || child.Top < -2 || child.Right > container.ClientSize.Width + 2 || child.Bottom > container.ClientSize.Height + 2))
                    yield return child.Name + " extends outside " + container.Name + ".";
                if (child is RoundedButton { AutoSize: true, Glyph: CaptionGlyph.None } button)
                {
                    Size preferred = button.GetPreferredSize(Size.Empty);
                    if (preferred.Height > button.Height + 2) yield return button.Name + " clips its button label vertically.";
                    if (preferred.Width > button.Width + 2) yield return button.Name + " clips its full button label horizontally.";
                }
                if (child is Label { AutoSize: true, AutoEllipsis: false } label)
                {
                    Size preferred = label.GetPreferredSize(new Size(label.Width, 0));
                    if (preferred.Height > label.Height + 2) yield return label.Name + " clips wrapped text.";
                }
            }
            if (container is not (TableLayoutPanel or FlowLayoutPanel)) continue;
            for (int i = 0; i < children.Length; i++)
            for (int j = i + 1; j < children.Length; j++)
            {
                Rectangle overlap = Rectangle.Intersect(children[i].Bounds, children[j].Bounds);
                if (overlap.Width > 2 && overlap.Height > 2)
                    yield return children[i].Name + " overlaps " + children[j].Name + ".";
            }
        }
    }

    private static IEnumerable<string> FindVisualIssues(Form form)
    {
        if (form.FormBorderStyle != FormBorderStyle.None) yield return "The legacy Windows title bar is still enabled.";
        long style = GetWindowLongPtr(form.Handle, -16).ToInt64();
        if ((style & 0x00CF0000) != 0) yield return "The native caption/frame style is still present.";
        if (form.Icon == null) yield return "The window has no application icon.";
        foreach (ComboBox combo in Theme.Descendants(form).OfType<ComboBox>())
            yield return combo.Name + " still uses the legacy native dropdown.";
        foreach (DarkTextView text in Theme.Descendants(form).OfType<DarkTextView>())
            if (text.Editor.ScrollBars != RichTextBoxScrollBars.None) yield return text.Name + " uses native text scrollbars.";
        foreach (DataGridView grid in Theme.Descendants(form).OfType<DataGridView>())
            if (grid.ScrollBars != ScrollBars.None) yield return grid.Name + " uses native grid scrollbars.";
        foreach (ScrollableControl scroll in Theme.Descendants(form).OfType<ScrollableControl>().Where(c => c.Visible))
            if (scroll.AutoScroll && (scroll.VerticalScroll.Visible || scroll.HorizontalScroll.Visible))
                yield return scroll.Name + " exposes a native page scrollbar.";
        RoundedButton? sample = Theme.Descendants(form).OfType<RoundedButton>().FirstOrDefault(button => button.Visible && button.Glyph == CaptionGlyph.None);
        if (sample != null && !CornersStayClean(sample)) yield return sample.Name + " leaves stale pixels in its corners after state changes.";
        var progress = Theme.Descendants(form).OfType<DarkProgressBar>().SingleOrDefault();
        if (progress == null) yield return "The progress control is not owner-painted.";
        else
        {
            if (progress.Height != progress.LogicalToDeviceUnits(UiPalette.ProgressHeight)) yield return "Progress height changed with layout.";
            using var bitmap = new Bitmap(Math.Max(1, progress.Width), Math.Max(1, progress.Height));
            progress.DrawToBitmap(bitmap, new Rectangle(Point.Empty, bitmap.Size));
            bool white = false;
            for (int y = 0; y < bitmap.Height && !white; y++)
            for (int x = 0; x < bitmap.Width; x++)
            {
                Color pixel = bitmap.GetPixel(x, y);
                if (pixel.R > 200 && pixel.G > 200 && pixel.B > 200) { white = true; break; }
            }
            if (white) yield return "Progress contains a white system-themed surface.";
        }
        foreach (Button button in Theme.Descendants(form).OfType<Button>().Where(button => button.Visible))
            if (button is not RoundedButton) yield return button.Name + " still uses native disabled text drawing.";
        foreach (RoundedButton button in Theme.Descendants(form).OfType<RoundedButton>())
            if (button.Glyph == CaptionGlyph.None && button.Symbol == UiIcon.None)
                yield return button.Name + " has no action icon.";
        var disabled = Theme.Descendants(form).OfType<RoundedButton>().FirstOrDefault(button => button.Visible && !button.Enabled && !string.IsNullOrEmpty(button.Text));
        if (disabled != null)
        {
            using var bitmap = new Bitmap(Math.Max(1, disabled.Width), Math.Max(1, disabled.Height));
            disabled.DrawToBitmap(bitmap, new Rectangle(Point.Empty, bitmap.Size));
            Color expected = disabled.PaintedTextColor;
            int textPixels = 0;
            for (int y = 2; y < bitmap.Height - 2; y++)
            for (int x = 2; x < bitmap.Width - 2; x++)
            {
                Color pixel = bitmap.GetPixel(x, y);
                if (Math.Abs(pixel.R - expected.R) < 12 && Math.Abs(pixel.G - expected.G) < 12 && Math.Abs(pixel.B - expected.B) < 12) textPixels++;
            }
            if (textPixels < 4) yield return disabled.Name + " did not render its readable disabled text color.";
        }
    }
    private static bool CornersStayClean(RoundedButton button)
    {
        bool active = button.Active, enabled = button.Enabled;
        Color expected = button.Parent?.BackColor ?? UiPalette.Background;
        try
        {
            using var bitmap = new Bitmap(Math.Max(1, button.Width), Math.Max(1, button.Height));
            foreach (var state in new[] { (true, true), (false, true), (false, false), (true, true) })
            {
                button.Active = state.Item1; button.Enabled = state.Item2;
                button.DrawToBitmap(bitmap, new Rectangle(Point.Empty, bitmap.Size));
                foreach (Point corner in new[] { Point.Empty, new Point(bitmap.Width - 1, 0),
                    new Point(0, bitmap.Height - 1), new Point(bitmap.Width - 1, bitmap.Height - 1) })
                {
                    Color actual = bitmap.GetPixel(corner.X, corner.Y);
                    if (Math.Abs(actual.R - expected.R) > 2 || Math.Abs(actual.G - expected.G) > 2 || Math.Abs(actual.B - expected.B) > 2) return false;
                }
            }
            return true;
        }
        finally { button.Active = active; button.Enabled = enabled; }
    }
    private static IEnumerable<string> CheckBorderlessButtons(Font font)
    {
        using var host = new BufferedPanel { Size = new Size(200, 80), BackColor = UiPalette.Background };
        using var button = new ButtonProbe { Size = new Size(160, 44), Font = font, Text = "", BackColor = UiPalette.Card };
        host.Controls.Add(button); host.CreateControl(); button.CreateControl();
        foreach (string state in new[] { "normal", "hover", "pressed", "selected", "disabled" })
        {
            button.SetState(state);
            using var bitmap = new Bitmap(button.Width, button.Height);
            button.DrawToBitmap(bitmap, new Rectangle(Point.Empty, bitmap.Size));
            Color fill = bitmap.GetPixel(bitmap.Width / 2, bitmap.Height / 2), back = host.BackColor;
            foreach (Point edge in new[] { new Point(bitmap.Width / 2, 0), new Point(bitmap.Width / 2, bitmap.Height - 1),
                new Point(0, bitmap.Height / 2), new Point(bitmap.Width - 1, bitmap.Height / 2) })
            {
                Color pixel = bitmap.GetPixel(edge.X, edge.Y);
                bool Between(byte value, byte a, byte b) => value >= Math.Min(a, b) - 2 && value <= Math.Max(a, b) + 2;
                if (!Between(pixel.R, fill.R, back.R) || !Between(pixel.G, fill.G, back.G) || !Between(pixel.B, fill.B, back.B))
                { yield return "Button " + state + " has a contrasting decorative outline."; break; }
            }
        }
    }
    private static IEnumerable<string> CheckPartialPaints(Font font)
    {
        Rectangle clip = new(8, 8, 24, 12);
        using var button = new ButtonProbe { Size = new Size(160, 44), Text = "Partial repaint clipping", Font = font };
        using var progress = new ProgressProbe { Size = new Size(160, 6), Value = 50 };
        foreach (bool isProgress in new[] { false, true })
        {
            using var bitmap = new Bitmap(160, 44);
            using (Graphics graphics = Graphics.FromImage(bitmap))
            {
                graphics.Clear(Color.Magenta);
                Rectangle dirty = isProgress ? new Rectangle(8, 0, 24, 6) : clip;
                graphics.SetClip(dirty);
                if (isProgress) progress.PaintPartial(graphics, dirty); else button.PaintPartial(graphics, dirty);
            }
            Rectangle valid = isProgress ? new Rectangle(8, 0, 24, 6) : clip;
            bool leaked = false;
            for (int y = 0; y < bitmap.Height && !leaked; y++)
            for (int x = 0; x < bitmap.Width; x++)
                if (!valid.Contains(x, y) && bitmap.GetPixel(x, y).ToArgb() != Color.Magenta.ToArgb()) { leaked = true; break; }
            if (leaked) yield return (isProgress ? "Progress" : "Button") + " painted outside its invalidated region.";
        }
    }
    private sealed class ProgressProbe : DarkProgressBar
    {
        internal void PaintPartial(Graphics graphics, Rectangle clip)
        { using var args = new PaintEventArgs(graphics, clip); OnPaintBackground(args); OnPaint(args); }
    }
    private sealed class ButtonProbe : RoundedButton
    {
        internal void PaintPartial(Graphics graphics, Rectangle clip)
        { using var args = new PaintEventArgs(graphics, clip); OnPaint(args); }
        internal void SetState(string state)
        {
            Enabled = true; Active = false;
            OnMouseUp(new MouseEventArgs(MouseButtons.Left, 1, 10, 10, 0));
            OnMouseLeave(EventArgs.Empty);
            if (state is "hover" or "pressed") OnMouseEnter(EventArgs.Empty);
            if (state == "pressed") OnMouseDown(new MouseEventArgs(MouseButtons.Left, 1, 10, 10, 0));
            if (state == "selected") Active = true;
            if (state == "disabled") Enabled = false;
        }
    }
    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
    private static extern nint GetWindowLongPtr(nint window, int index);

}
