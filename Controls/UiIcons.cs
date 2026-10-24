using System.Drawing.Drawing2D;

namespace BoosterX.App.Controls;

internal static class UiIcons
{
    internal static void Draw(Graphics graphics, Rectangle bounds, UiIcon icon, Color color)
    {
        if (bounds.Width < 1 || bounds.Height < 1 || !UiIconVectors.Shapes.TryGetValue(icon, out IconPrimitive[]? shapes)) return;
        var saved = graphics.Save();
        try
        {
            // Intersect with the existing WM_PAINT clip; never replace a partial invalidation.
            graphics.SetClip(bounds, CombineMode.Intersect);
            graphics.TranslateTransform(bounds.Left, bounds.Top);
            graphics.ScaleTransform(bounds.Width / 24f, bounds.Height / 24f);
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using var pen = new Pen(color, 1.8f) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round };
            foreach (IconPrimitive shape in shapes)
            {
                float[] p = shape.Points;
                if (shape.Kind == IconPrimitiveKind.Ellipse) graphics.DrawEllipse(pen, p[0], p[1], p[2], p[3]);
                else if (shape.Kind == IconPrimitiveKind.Rectangle) graphics.DrawRectangle(pen, p[0], p[1], p[2], p[3]);
                else
                {
                    PointF[] points = Enumerable.Range(0, p.Length / 2).Select(i => new PointF(p[i * 2], p[i * 2 + 1])).ToArray();
                    if (shape.Kind == IconPrimitiveKind.Bezier) graphics.DrawBeziers(pen, points);
                    else graphics.DrawLines(pen, points);
                }
            }
        }
        finally { graphics.Restore(saved); }
    }

    internal static ToolStripMenuItem AddMenuItem(ToolStripItemCollection items, string text, UiIcon icon, EventHandler click)
    {
        var item = new ToolStripMenuItem(text) { Tag = icon };
        item.Click += click;
        items.Add(item);
        return item;
    }
}

internal sealed class VectorIcon : Control
{
    [System.ComponentModel.DefaultValue(UiIcon.None)]
    internal UiIcon Symbol { get; set; }
    internal VectorIcon()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer
            | ControlStyles.ResizeRedraw | ControlStyles.Opaque, true);
        TabStop = false;
    }
    protected override void OnPaint(PaintEventArgs e)
    {
        UiDrawing.ControlBackground(e.Graphics, ClientRectangle, Parent?.BackColor ?? UiPalette.Background);
        int side = Math.Min(Width, Height);
        UiIcons.Draw(e.Graphics, new Rectangle((Width - side) / 2, (Height - side) / 2, side, side), Symbol, ForeColor);
    }
}
