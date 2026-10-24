using System.Drawing;

namespace BoosterX.App.Controls;

internal readonly record struct IconContent(Rectangle Icon, Rectangle Text);

internal static class IconGeometry
{
    internal static Size Measure(Size text, int iconSize, int gap)
        => new(Math.Max(0, text.Width) + Math.Max(0, iconSize) + (text.Width > 0 && iconSize > 0 ? Math.Max(0, gap) : 0),
            Math.Max(Math.Max(0, text.Height), Math.Max(0, iconSize)));

    // alignment: -1 = left, 0 = center, 1 = right. Both rectangles stay inside the content box.
    internal static IconContent Arrange(Rectangle content, Size text, int iconSize, int gap, int alignment)
    {
        int width = Math.Max(0, content.Width), height = Math.Max(0, content.Height);
        int side = Math.Min(Math.Max(0, iconSize), Math.Min(width, height));
        int separation = text.Width > 0 && side > 0 ? Math.Min(Math.Max(0, gap), width - side) : 0;
        int textWidth = Math.Min(Math.Max(0, text.Width), width - side - separation);
        int groupWidth = side + separation + textWidth;
        int left = content.Left + (alignment < 0 ? 0 : alignment > 0 ? width - groupWidth : (width - groupWidth) / 2);
        return new(new Rectangle(left, content.Top + (height - side) / 2, side, side),
            new Rectangle(left + side + separation, content.Top, textWidth, height));
    }
}
