using BoosterX.App.Controls;
using System.Drawing.Imaging;

namespace BoosterX.App.Services;

internal sealed record RenderingCheckResult(string Name, bool Passed, string Details);

/// <summary>Exercises the actual opaque ButtonBase foreground pass, without manually clearing its background first.</summary>
internal static class ControlRenderingChecks
{
    // Standalone fixtures only. No optimizer, registry, network, files or services are changed by the controls.
    private const int FrameWidth = 240;
    private const int FrameHeight = 44;
    private const int AtlasGap = 12;
    private const int PixelTolerance = 2;
    private static readonly string[] States = ["normal", "hover", "pressed", "selected", "disabled", "caption"];
    private static readonly string[] Captions = ["Restore latest setting batch", "Refresh selected details", "Run as administrator", "Stop after current action"];

    internal static IReadOnlyList<RenderingCheckResult> Run(Font font, string? directory = null)
    {
        var results = new List<RenderingCheckResult>();
        using var parent = new BufferedPanel { Size = new Size(FrameWidth + 20, FrameHeight + 20), BackColor = UiPalette.Background };
        using var button = new ButtonProbe { Size = new Size(FrameWidth, FrameHeight), Font = font, Text = "Action button", Padding = new Padding(12, 7, 12, 7) };
        parent.Controls.Add(button);
        using var atlas = new Bitmap(FrameWidth + 2 * AtlasGap, States.Length * (FrameHeight + AtlasGap) + AtlasGap);
        using (Graphics graphics = Graphics.FromImage(atlas)) graphics.Clear(parent.BackColor);
        for (int index = 0; index < States.Length; index++)
        {
            string state = States[index];
            button.State(state);
            using Bitmap frame = DirtyFrame(button.Size, (g, clip) => button.Foreground(g, clip));
            Check(results, "Opaque foreground clears corners: " + state, () => AssertCorners(frame, parent.BackColor));
            using Graphics graphics = Graphics.FromImage(atlas);
            graphics.DrawImageUnscaled(frame, AtlasGap, AtlasGap + index * (FrameHeight + AtlasGap));
        }
        Check(results, "Foreground repaint obeys the invalidated region", () =>
        {
            button.State("normal");
            Rectangle dirty = new(6, 6, 28, 16);
            using var frame = new Bitmap(button.Width, button.Height);
            using (Graphics graphics = Graphics.FromImage(frame))
            {
                graphics.Clear(Color.Magenta); graphics.SetClip(dirty); button.Foreground(graphics, dirty);
            }
            for (int y = 0; y < frame.Height; y++)
            for (int x = 0; x < frame.Width; x++)
                if (!dirty.Contains(x, y) && frame.GetPixel(x, y).ToArgb() != Color.Magenta.ToArgb())
                    throw new InvalidOperationException("Paint escaped the invalidated rectangle.");
        });
        Check(results, "A 1px control still clears its opaque surface", () =>
        {
            using var tiny = new ButtonProbe { Size = new Size(1, 1) };
            parent.Controls.Add(tiny);
            using Bitmap frame = DirtyFrame(tiny.Size, (g, clip) => tiny.Foreground(g, clip));
            AssertColor(frame.GetPixel(0, 0), parent.BackColor);
        });
        foreach (Appearance appearance in new[] { Appearance.Normal, Appearance.Button })
        {
            using var check = new CheckProbe { Size = new Size(FrameWidth, FrameHeight), Appearance = appearance, Font = font, Text = "Option" };
            parent.Controls.Add(check);
            Check(results, "Checkbox foreground clears corners: " + appearance, () =>
            {
                using Bitmap frame = DirtyFrame(check.Size, (g, clip) => check.Foreground(g, clip));
                AssertCorners(frame, parent.BackColor);
            });
        }
        foreach (float size in new[] { 10f, 15f, 20f })
        {
            using var scaled = new Font(font.FontFamily, size, FontStyle.Regular);
            Check(results, $"Full preferred captions at {size}pt", () =>
            {
                foreach (string caption in Captions)
                {
                    using var action = new RoundedButton { Font = scaled, AutoSize = true, MinimumSize = new Size(130, 40), Padding = new Padding(12, 7, 12, 7), Text = caption };
                    Size measured = TextRenderer.MeasureText(caption, scaled, new Size(32767, 32767),
                        TextFormatFlags.SingleLine | TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix);
                    if (action.PreferredSize.Width < measured.Width + action.Padding.Horizontal)
                        throw new InvalidOperationException(caption + " has an ellipsized preferred width.");
                }
            });
        }
        Check(results, "Auto-sized action bars retain full captions", () => CheckFlow(font, directory));
        Check(results, "Icon buttons keep opaque corners clean in every state", () =>
        {
            button.Symbol = UiIcon.Shield;
            foreach (string state in States)
            {
                button.State(state);
                using Bitmap frame = DirtyFrame(button.Size, (g, clip) => button.Foreground(g, clip));
                AssertCorners(frame, parent.BackColor);
            }
            button.Symbol = UiIcon.None;
        });
        Check(results, "Icon buttons reserve text, icon and gap at every font size", () =>
        {
            foreach (float size in new[] { 10f, 15f, 20f })
            foreach (string caption in Captions)
            {
                using var scaled = new Font(font.FontFamily, size, FontStyle.Regular);
                using var action = new RoundedButton { AutoSize = true, Font = scaled, Text = caption,
                    Padding = new Padding(12, 7, 12, 7), Symbol = UiIcon.Shield };
                Size measured = UiDrawing.MeasureButtonText(caption, scaled, false);
                int required = measured.Width + action.Padding.Horizontal + action.LogicalToDeviceUnits(UiPalette.ActionIconSize + UiPalette.ActionIconGap);
                if (action.PreferredSize.Width < required || action.MinimumSize.Width < required)
                    throw new InvalidOperationException("Icon overlaps or truncates " + caption + " at " + size + "pt.");
            }
        });
        Check(results, "Vector drawing preserves partial clips and graphics transforms", () =>
        {
            using var frame = new Bitmap(FrameWidth, FrameHeight);
            using Graphics graphics = Graphics.FromImage(frame);
            graphics.Clear(Color.Magenta);
            Rectangle dirty = new(6, 6, 10, 12);
            graphics.SetClip(dirty);
            using var transform = graphics.Transform;
            RectangleF clipBefore = graphics.ClipBounds;
            foreach (UiIcon icon in Enum.GetValues<UiIcon>())
                UiIcons.Draw(graphics, new Rectangle(4, 4, 24, 24), icon, UiPalette.Text);
            using var after = graphics.Transform;
            if (!transform.Elements.SequenceEqual(after.Elements) || graphics.ClipBounds != clipBefore)
                throw new InvalidOperationException("Vector drawing changed the caller's graphics state.");
            for (int y = 0; y < frame.Height; y++)
            for (int x = 0; x < frame.Width; x++)
                if (!dirty.Contains(x, y) && frame.GetPixel(x, y).ToArgb() != Color.Magenta.ToArgb())
                    throw new InvalidOperationException("An icon escaped the invalidated region.");
        });
        Check(results, "Every vector icon paints at small and high-DPI sizes", () =>
        {
            foreach (UiIcon icon in Enum.GetValues<UiIcon>().Where(icon => icon != UiIcon.None))
            foreach (int side in new[] { 16, 36 })
            {
                using var frame = new Bitmap(side, side);
                using (Graphics graphics = Graphics.FromImage(frame))
                {
                    graphics.Clear(UiPalette.Background);
                    UiIcons.Draw(graphics, new Rectangle(0, 0, side, side), icon, UiPalette.Text);
                }
                int pixels = 0;
                for (int y = 0; y < side; y++)
                for (int x = 0; x < side; x++) if (frame.GetPixel(x, y).R > 100) pixels++;
                if (pixels < 4) throw new InvalidOperationException(icon + " did not paint at " + side + "px.");
            }
        });
        if (directory != null)
        {
            Directory.CreateDirectory(directory);
            atlas.Save(Path.Combine(directory, "button-states.png"), ImageFormat.Png);
        }
        return results;
    }
    private static void CheckFlow(Font font, string? directory)
    {
        using var form = new Form { ShowInTaskbar = false, StartPosition = FormStartPosition.Manual, Location = new Point(-30000, -30000), ClientSize = new Size(1100, 120), Font = font };
        using var flow = new BufferedFlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = true, BackColor = UiPalette.Background };
        form.Controls.Add(flow);
        foreach (string caption in Captions)
            flow.Controls.Add(new RoundedButton { AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink,
                MinimumSize = new Size(130, 40), Padding = new Padding(12, 7, 12, 7), Text = caption, Margin = new Padding(0, 0, 8, 8) });
        form.Show(); flow.PerformLayout();
        foreach (RoundedButton action in flow.Controls)
        {
            Size measured = TextRenderer.MeasureText(action.Text, action.Font, new Size(32767, 32767),
                TextFormatFlags.SingleLine | TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix);
            if (action.Width < measured.Width + action.Padding.Horizontal)
                throw new InvalidOperationException(action.Text + " is clipped by its actual layout bounds.");
        }
        var first = (RoundedButton)flow.Controls[0]; int clicks = 0;
        first.Click += (_, _) => clicks++;
        first.PerformClick(); first.Enabled = false; first.PerformClick();
        if (clicks != 1) throw new InvalidOperationException("Button click/disabled behavior changed.");
        first.Enabled = true;
        if (directory != null)
        {
            Directory.CreateDirectory(directory);
            using var bitmap = new Bitmap(flow.Width, flow.Height);
            flow.DrawToBitmap(bitmap, new Rectangle(Point.Empty, bitmap.Size));
            bitmap.Save(Path.Combine(directory, "action-labels.png"), ImageFormat.Png);
        }
        form.Close();
    }
    private static Bitmap DirtyFrame(Size size, Action<Graphics, Rectangle> paint)
    {
        var bitmap = new Bitmap(Math.Max(1, size.Width), Math.Max(1, size.Height));
        using Graphics graphics = Graphics.FromImage(bitmap);
        graphics.Clear(UiPalette.Accent); // Same kind of stale red backing pixels as the supplied screenshot.
        paint(graphics, new Rectangle(Point.Empty, bitmap.Size));
        return bitmap;
    }
    private static void AssertCorners(Bitmap bitmap, Color expected)
    {
        foreach (Point point in new[] { Point.Empty, new Point(bitmap.Width - 1, 0),
            new Point(0, bitmap.Height - 1), new Point(bitmap.Width - 1, bitmap.Height - 1) })
            AssertColor(bitmap.GetPixel(point.X, point.Y), expected);
    }
    private static void AssertColor(Color actual, Color expected)
    {
        if (Math.Abs(actual.R - expected.R) > PixelTolerance || Math.Abs(actual.G - expected.G) > PixelTolerance || Math.Abs(actual.B - expected.B) > PixelTolerance)
            throw new InvalidOperationException($"Stale corner: RGB {actual.R},{actual.G},{actual.B}; expected {expected.R},{expected.G},{expected.B}.");
    }
    private static void Check(List<RenderingCheckResult> results, string name, Action run)
    {
        try { run(); results.Add(new(name, true, "Passed")); }
        catch (Exception ex) { results.Add(new(name, false, ex.Message)); }
    }
    private sealed class CheckProbe : DarkCheckBox
    {
        internal void Foreground(Graphics graphics, Rectangle clip) { using var args = new PaintEventArgs(graphics, clip); OnPaint(args); }
    }
    private sealed class ButtonProbe : RoundedButton
    {
        internal void Foreground(Graphics graphics, Rectangle clip) { using var args = new PaintEventArgs(graphics, clip); OnPaint(args); }
        internal void State(string value)
        {
            Enabled = true; Active = false; Glyph = CaptionGlyph.None;
            OnMouseUp(new MouseEventArgs(MouseButtons.Left, 1, 10, 10, 0)); OnMouseLeave(EventArgs.Empty);
            if (value is "hover" or "pressed") OnMouseEnter(EventArgs.Empty);
            if (value == "pressed") OnMouseDown(new MouseEventArgs(MouseButtons.Left, 1, 10, 10, 0));
            if (value == "selected") Active = true;
            if (value == "disabled") Enabled = false;
            if (value == "caption") Glyph = CaptionGlyph.Close;
        }
    }
}
