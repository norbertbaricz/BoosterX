using System.Drawing;

namespace BoosterX.App.Controls;

[Flags]
internal enum ResizeEdges { None = 0, Left = 1, Right = 2, Top = 4, Bottom = 8 }

internal readonly record struct ScrollGeometry(int Range, int ThumbLength, int Travel)
{
    internal static ScrollGeometry Create(int track, int viewport, int extent, int minimumThumb)
    {
        track = Math.Max(0, track); viewport = Math.Max(0, viewport); extent = Math.Max(0, extent);
        int range = (int)Math.Clamp((long)extent - viewport, 0, int.MaxValue);
        int thumb = range == 0 || extent == 0 ? track : (int)Math.Clamp((long)track * viewport / extent, Math.Min(track, minimumThumb), track);
        return new(range, thumb, track - thumb);
    }
    internal int Position(int value) => Range == 0 ? 0 : (int)((long)Math.Clamp(value, 0, Range) * Travel / Range);
    internal int Offset(int pixel) => Travel == 0 ? 0 : (int)((long)Math.Clamp(pixel, 0, Travel) * Range / Travel);
}

internal static class WindowGeometry
{
    internal static ResizeEdges Hit(Point point, Size size, int border)
    {
        if (point.X < 0 || point.Y < 0 || point.X >= size.Width || point.Y >= size.Height) return ResizeEdges.None;
        ResizeEdges result = ResizeEdges.None;
        if (point.X < border) result |= ResizeEdges.Left;
        else if (point.X >= size.Width - border) result |= ResizeEdges.Right;
        if (point.Y < border) result |= ResizeEdges.Top;
        else if (point.Y >= size.Height - border) result |= ResizeEdges.Bottom;
        return result;
    }
    internal static Rectangle Resize(Rectangle initial, Point delta, ResizeEdges edges, Size minimum)
    {
        int width = initial.Width, height = initial.Height, left = initial.Left, top = initial.Top;
        if (edges.HasFlag(ResizeEdges.Left)) { width = Math.Max(minimum.Width, initial.Width - delta.X); left = initial.Right - width; }
        if (edges.HasFlag(ResizeEdges.Right)) width = Math.Max(minimum.Width, initial.Width + delta.X);
        if (edges.HasFlag(ResizeEdges.Top)) { height = Math.Max(minimum.Height, initial.Height - delta.Y); top = initial.Bottom - height; }
        if (edges.HasFlag(ResizeEdges.Bottom)) height = Math.Max(minimum.Height, initial.Height + delta.Y);
        return new(left, top, width, height);
    }
}

internal readonly record struct PercentMinimum(double Weight, int Minimum);

internal static class ViewportGeometry
{
    /// <summary>Reserves enough shared space for every weighted row, rather than summing their minima.</summary>
    internal static int RequiredExtent(int fixedHeight, IReadOnlyList<PercentMinimum> rows)
    {
        double totalWeight = rows.Where(r => r.Weight > 0 && double.IsFinite(r.Weight)).Sum(r => r.Weight);
        double shared = 0;
        foreach (PercentMinimum row in rows)
            if (row.Weight > 0 && double.IsFinite(row.Weight))
                shared = Math.Max(shared, Math.Max(0, row.Minimum) * totalWeight / row.Weight);
        return (int)Math.Clamp(Math.Ceiling(Math.Max(0, fixedHeight) + shared), 0, int.MaxValue);
    }
}
