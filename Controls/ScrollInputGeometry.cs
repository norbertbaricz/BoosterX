using System.Drawing;

namespace BoosterX.App.Controls;

internal readonly record struct WheelMove(bool Accepted, int Value);

internal struct WheelAccumulator
{
    private long remainder;
    private bool horizontal;
    internal WheelMove Move(int value, int maximum, int delta, int unit, int page, int lines, bool across)
    {
        maximum = Math.Max(0, maximum); value = Math.Clamp(value, 0, maximum);
        if (horizontal != across) { remainder = 0; horizontal = across; }
        if (maximum == 0 || lines == 0 || delta == 0 || value == 0 && delta > 0 || value == maximum && delta < 0)
        { remainder = 0; return new(false, value); }
        remainder += delta;
        long notches = remainder / UiPalette.WheelDelta;
        remainder %= UiPalette.WheelDelta;
        long step = lines < 0 ? Math.Max(1, page) : Math.Min(int.MaxValue, (long)Math.Max(1, unit) * lines);
        return new(true, (int)Math.Clamp(value - notches * step, 0, maximum));
    }
}

internal readonly record struct PopupPlacement(Rectangle Bounds, int VisibleItems);

internal static class DropDownGeometry
{
    internal static PopupPlacement Place(Rectangle owner, Rectangle working, int desiredWidth, int itemHeight, int count, int padding, int maxItems)
    {
        itemHeight = Math.Max(1, itemHeight); padding = Math.Max(0, padding);
        int below = Math.Max(0, working.Bottom - owner.Bottom), above = Math.Max(0, owner.Top - working.Top);
        int desiredHeight = (int)Math.Min(int.MaxValue, (long)Math.Clamp(count, 1, Math.Max(1, maxItems)) * itemHeight + 2L * padding);
        bool down = desiredHeight <= below || below >= above;
        int room = Math.Max(1, down ? below : above);
        int height = Math.Min(Math.Max(1, working.Height), Math.Min(desiredHeight, room));
        int width = Math.Clamp(Math.Max(owner.Width, desiredWidth), 1, Math.Max(1, working.Width));
        int x = Math.Clamp(owner.Left, working.Left, working.Right - width);
        int y = Math.Clamp(down ? owner.Bottom : owner.Top - height, working.Top, working.Bottom - height);
        return new(new(x, y, width, height), Math.Max(1, (height - 2 * padding) / itemHeight));
    }
    internal static int HitRow(int y, int offset, int height, int count)
    {
        if (y < 0 || height <= 0 || count <= 0) return -1;
        long row = ((long)y + Math.Max(0, offset)) / height;
        return row < count ? (int)row : -1;
    }
}

internal readonly record struct ScrollRow(int Index, int Height);
internal static class GridScrollGeometry
{
    internal static int Extent(IReadOnlyList<ScrollRow> rows)
        => (int)Math.Min(int.MaxValue, rows.Sum(row => (long)Math.Max(1, row.Height)));
    internal static int Offset(IReadOnlyList<ScrollRow> rows, int index)
    {
        if (index < 0) return 0;
        long offset = 0;
        foreach (ScrollRow row in rows)
        {
            if (row.Index == index) break;
            offset += Math.Max(1, row.Height);
        }
        return (int)Math.Min(int.MaxValue, offset);
    }
    internal static int RowAt(IReadOnlyList<ScrollRow> rows, int offset, int viewport)
    {
        if (rows.Count == 0) return -1;
        int maximum = Math.Max(0, Extent(rows) - Math.Max(1, viewport));
        offset = Math.Clamp(offset, 0, maximum);
        long cursor = 0;
        for (int i = 0; i < rows.Count; i++)
        {
            long end = cursor + Math.Max(1, rows[i].Height);
            if (offset < end)
                return offset == maximum && offset > cursor && i + 1 < rows.Count ? rows[i + 1].Index : rows[i].Index;
            cursor = end;
        }
        return rows[^1].Index;
    }
}
