using System.Drawing;

namespace BoosterX.App.Controls;

internal readonly record struct DetailRowBounds(Rectangle Action, Rectangle Description, int Height);

internal static class DetailListGeometry
{
    internal static bool Stacked(int width,int actionWidth,int dpi)
        => width < Scale(UiPalette.DetailStackWidth,dpi)
            || width-actionWidth-Scale(UiPalette.SectionGap,dpi) < Scale(UiPalette.DetailDescriptionMinimum,dpi);

    internal static DetailRowBounds Arrange(int width,int actionWidth,int actionHeight,int descriptionHeight,int dpi)
    {
        width = Math.Max(1,width); actionHeight = Math.Max(1,actionHeight); descriptionHeight = Math.Max(1,descriptionHeight);
        int gap = Scale(UiPalette.SectionGap,dpi);
        if (Stacked(width,actionWidth,dpi))
            return new(new(0,0,width,actionHeight),new(0,actionHeight+gap,width,descriptionHeight),actionHeight+gap+descriptionHeight);
        int actualWidth = Math.Clamp(actionWidth,1,width-1);
        return new(new(0,0,actualWidth,actionHeight),new(actualWidth+gap,0,Math.Max(1,width-actualWidth-gap),descriptionHeight),Math.Max(actionHeight,descriptionHeight));
    }
    internal static int Scale(int logical,int dpi) => Math.Max(1,(int)Math.Round(logical*Math.Max(1,dpi)/96d));
}
