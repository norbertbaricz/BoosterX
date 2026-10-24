using System.Drawing.Drawing2D;

namespace BoosterX.App.Controls;

internal static class UiDrawing
{
    internal static GraphicsPath Round(RectangleF bounds, float radius)
    {
        var path = new GraphicsPath();
        if (bounds.Width <= 0 || bounds.Height <= 0) return path;
        float diameter = Math.Min(Math.Max(radius * 2, 1), Math.Min(bounds.Width, bounds.Height));
        path.AddArc(bounds.Left, bounds.Top, diameter, diameter, 180, 90);
        path.AddArc(bounds.Right - diameter, bounds.Top, diameter, diameter, 270, 90);
        path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
        path.AddArc(bounds.Left, bounds.Bottom - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();
        return path;
    }

    internal static void ControlBackground(Graphics graphics, Rectangle bounds, Color color)
    {
        using var brush = new SolidBrush(color);
        graphics.FillRectangle(brush, bounds);
    }
    internal static Size MeasureButtonText(string text, Font font, bool mnemonic)
    {
        // Measurement must be unbounded: ellipsis/alignment flags are for painting, not natural size.
        TextFormatFlags flags = TextFormatFlags.SingleLine | TextFormatFlags.NoPadding;
        if (!mnemonic) flags |= TextFormatFlags.NoPrefix;
        return TextRenderer.MeasureText(text, font,
            new Size(UiPalette.MaximumTextMeasureSize, UiPalette.MaximumTextMeasureSize), flags);
    }

    internal static void FillRound(Graphics graphics, Rectangle bounds, int radius, Color color)
    {
        if (bounds.Width <= 0 || bounds.Height <= 0) return;
        var saved = graphics.Save();
        try
        {
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using var path = Round(bounds, radius);
            using var brush = new SolidBrush(color);
            graphics.FillPath(brush, path);
        }
        finally { graphics.Restore(saved); }
    }
    internal static void FocusCue(Graphics graphics, Rectangle bounds, int dpi, int logicalInset = UiPalette.FocusCueInset)
    {
        int width = Math.Min(bounds.Width, UiPalette.FocusCueWidth * dpi / 96);
        int height = Math.Max(1, UiPalette.FocusCueHeight * dpi / 96);
        int inset = logicalInset * dpi / 96;
        if (width < 1 || bounds.Height < height + inset) return;
        using var brush = new SolidBrush(UiPalette.Focus);
        graphics.FillRectangle(brush, bounds.Left + (bounds.Width - width) / 2, bounds.Bottom - height - inset, width, height);
    }
    internal static TextFormatFlags ButtonTextFlags(ContentAlignment alignment, bool mnemonic)
    {
        TextFormatFlags flags = TextFlags(alignment, mnemonic) & ~TextFormatFlags.WordBreak;
        return flags | TextFormatFlags.SingleLine | TextFormatFlags.NoPadding;
    }

    internal static void Check(Graphics graphics, Rectangle box, CheckState state, bool enabled)
    {
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using var shape = Round(new RectangleF(box.X + 0.5f, box.Y + 0.5f, box.Width - 1, box.Height - 1), box.Width / 5f);
        Color fill = state != CheckState.Unchecked && enabled ? UiPalette.Accent : UiPalette.Card;
        using var brush = new SolidBrush(fill);
        using var border = new Pen(enabled ? state == CheckState.Unchecked ? UiPalette.Muted : UiPalette.Accent : UiPalette.Border);
        graphics.FillPath(brush, shape); graphics.DrawPath(border, shape);
        using var tick = new Pen(enabled ? UiPalette.Text : UiPalette.DisabledText, Math.Max(1.7f, box.Width / 9f))
        { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round };
        if (state == CheckState.Checked)
            graphics.DrawLines(tick, [new PointF(box.Left + box.Width * 0.22f, box.Top + box.Height * 0.51f),
                new PointF(box.Left + box.Width * 0.43f, box.Top + box.Height * 0.72f), new PointF(box.Left + box.Width * 0.79f, box.Top + box.Height * 0.29f)]);
        else if (state == CheckState.Indeterminate)
            graphics.DrawLine(tick, box.Left + box.Width * 0.25f, box.Top + box.Height * 0.5f,
                box.Left + box.Width * 0.75f, box.Top + box.Height * 0.5f);
    }

    internal static TextFormatFlags TextFlags(ContentAlignment alignment, bool mnemonic)
    {
        TextFormatFlags flags = TextFormatFlags.EndEllipsis | TextFormatFlags.WordBreak
            | TextFormatFlags.PreserveGraphicsClipping | TextFormatFlags.PreserveGraphicsTranslateTransform;
        flags |= alignment is ContentAlignment.TopCenter or ContentAlignment.MiddleCenter or ContentAlignment.BottomCenter
            ? TextFormatFlags.HorizontalCenter : alignment is ContentAlignment.TopRight or ContentAlignment.MiddleRight or ContentAlignment.BottomRight
            ? TextFormatFlags.Right : TextFormatFlags.Left;
        flags |= alignment is ContentAlignment.MiddleLeft or ContentAlignment.MiddleCenter or ContentAlignment.MiddleRight
            ? TextFormatFlags.VerticalCenter : alignment is ContentAlignment.BottomLeft or ContentAlignment.BottomCenter or ContentAlignment.BottomRight
            ? TextFormatFlags.Bottom : TextFormatFlags.Top;
        if (!mnemonic) flags |= TextFormatFlags.NoPrefix;
        return flags;
    }

    internal static void Text(Graphics graphics, string text, Font font, Rectangle bounds, Color color, ContentAlignment alignment, bool mnemonic)
    {
        TextFormatFlags flags = TextFlags(alignment, mnemonic) & ~(TextFormatFlags.VerticalCenter | TextFormatFlags.Bottom);
        Size measured = TextRenderer.MeasureText(graphics, text, font, new Size(Math.Max(1, bounds.Width), int.MaxValue), flags);
        int height = Math.Min(bounds.Height, measured.Height);
        if (alignment is ContentAlignment.MiddleLeft or ContentAlignment.MiddleCenter or ContentAlignment.MiddleRight)
            bounds.Y += Math.Max(0, (bounds.Height - height) / 2);
        else if (alignment is ContentAlignment.BottomLeft or ContentAlignment.BottomCenter or ContentAlignment.BottomRight)
            bounds.Y += Math.Max(0, bounds.Height - height);
        bounds.Height = height;
        TextRenderer.DrawText(graphics, text, font, bounds, color, flags);
    }
}
