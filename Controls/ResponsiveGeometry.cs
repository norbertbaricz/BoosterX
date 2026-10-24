using System.Drawing;

namespace BoosterX.App.Controls;

/// <summary>Easy-to-tune layout defaults in logical pixels at 96 DPI.</summary>
internal static class UiLayout
{
    internal const int DefaultWidth = 1280;
    internal const int DefaultHeight = 720;
    internal const int MinimumWidth = 640;
    internal const int MinimumHeight = 400;
    internal const int ScreenInset = 16;
    internal const int SidebarWidth = 244;
    internal const int MediumSidebarWidth = 220;
    internal const int SmallSidebarWidth = 192;
    internal const int SidebarGap = 16;
    internal const int WideBreakpoint = 1100;
    internal const int HeaderStackBreakpoint = 1080;
    internal const int FooterStackBreakpoint = 900;
    internal const int MenuBreakpoint = 760;
    internal const int ShortHeightBreakpoint = 360;
    internal const int BrandLabelBreakpoint = 480;
    internal const int BrandIconBreakpoint = 360;
    internal const int CaptionCollapseBreakpoint = 320;
    internal const int ContentMinimumWidth = 520;
    internal const int NormalPaddingHorizontal = 16;
    internal const int NormalPaddingVertical = 12;
    internal const int CompactPadding = 8;
    internal const int CaptionWidth = 40;
    internal const int CompactCaptionWidth = 32;
    internal const int BodyGap = 8;
    internal const string StopText = "Stop after current action";
    internal const string CompactStopText = "Stop after action";
}

internal readonly record struct HeaderArrangement(bool CommandMenu, bool Stacked, Size Tools, Size Captions);

internal static class HeaderGeometry
{
    // Reserve the natural, single-row sizes before the table allocates its percent column.
    // A toolbar must never silently wrap inside a row whose height was already measured.
    internal static HeaderArrangement Arrange(int availableWidth, Size brand, Size actions, Size menu, Size captions,
        int gap, bool preferMenu, bool preferStacked)
    {
        availableWidth = Math.Max(1, availableWidth);
        bool commandMenu = preferMenu || actions.Width + gap > availableWidth;
        Size tools = commandMenu ? menu : actions;
        bool stacked = preferStacked || (long)brand.Width + tools.Width + captions.Width + 2L * gap > availableWidth;
        return new(commandMenu, stacked, tools, captions);
    }
}

internal readonly record struct ResponsiveMetrics(int SidebarWidth, int SidebarGap, bool StackedHeader,
    bool StackedFooter, bool CommandMenu, bool ShortHeight, bool ShowBrandLabel, bool ShowBrandIcon,
    bool CompactCaptions, int CaptionWidth, int PaddingHorizontal, int PaddingVertical);

internal static class ResponsiveGeometry
{
    internal static int Scale(int logical, int dpi) => (int)(((long)Math.Max(0, logical) * Math.Clamp(dpi, 48, 768) + 48) / 96);
    internal static int Logical(int pixels, int dpi) => (int)((long)Math.Max(0, pixels) * 96 / Math.Clamp(dpi, 48, 768));
    internal static Rectangle Available(Rectangle working, int dpi)
    {
        int width = Math.Max(1, working.Width), height = Math.Max(1, working.Height);
        int margin = Scale(UiLayout.ScreenInset, dpi);
        int x = Math.Min(margin, (width - 1) / 2), y = Math.Min(margin, (height - 1) / 2);
        return new Rectangle(working.X + x, working.Y + y, width - 2 * x, height - 2 * y);
    }
    internal static Size Minimum(Rectangle working, int dpi)
    {
        Size available = Available(working, dpi).Size;
        return new Size(Math.Min(Scale(UiLayout.MinimumWidth, dpi), available.Width),
            Math.Min(Scale(UiLayout.MinimumHeight, dpi), available.Height));
    }
    internal static Rectangle Initial(Rectangle working, int dpi)
    {
        Rectangle available = Available(working, dpi);
        int width = Math.Min(Scale(UiLayout.DefaultWidth, dpi), available.Width);
        int height = Math.Min(Scale(UiLayout.DefaultHeight, dpi), available.Height);
        return new Rectangle(available.X + (available.Width - width) / 2, available.Y + (available.Height - height) / 2, width, height);
    }
    internal static Rectangle Fit(Rectangle current, Rectangle working, int dpi)
    {
        Rectangle available = Available(working, dpi); Size minimum = Minimum(working, dpi);
        int width = Math.Clamp(current.Width, minimum.Width, available.Width);
        int height = Math.Clamp(current.Height, minimum.Height, available.Height);
        return new Rectangle(Math.Clamp(current.X, available.Left, available.Right - width),
            Math.Clamp(current.Y, available.Top, available.Bottom - height), width, height);
    }
    internal static ResponsiveMetrics Layout(Size client, int dpi)
    {
        int width = Logical(client.Width, dpi), height = Logical(client.Height, dpi);
        bool shortHeight = height < UiLayout.ShortHeightBreakpoint;
        bool menu = width < UiLayout.MenuBreakpoint || shortHeight;
        int sidebar = menu ? 0 : width >= UiLayout.WideBreakpoint ? UiLayout.SidebarWidth
            : width >= UiLayout.FooterStackBreakpoint ? UiLayout.MediumSidebarWidth : UiLayout.SmallSidebarWidth;
        bool compact = width < UiLayout.BrandLabelBreakpoint;
        return new ResponsiveMetrics(Scale(sidebar, dpi), sidebar == 0 ? 0 : Scale(UiLayout.SidebarGap, dpi),
            !menu && width < UiLayout.HeaderStackBreakpoint, width < UiLayout.FooterStackBreakpoint,
            menu, shortHeight, width >= UiLayout.BrandLabelBreakpoint, width >= UiLayout.BrandIconBreakpoint,
            width < UiLayout.CaptionCollapseBreakpoint,
            Scale(compact ? UiLayout.CompactCaptionWidth : UiLayout.CaptionWidth, dpi),
            Scale(compact ? UiLayout.CompactPadding : UiLayout.NormalPaddingHorizontal, dpi),
            Scale(compact || shortHeight ? UiLayout.CompactPadding : UiLayout.NormalPaddingVertical, dpi));
    }
    internal static int Reveal(int offset, int viewport, int start, int length, int extent)
    {
        viewport = Math.Max(1, viewport); length = Math.Max(0, length);
        long end = (long)start + length;
        long next = offset;
        if (length <= viewport)
        {
            if (start < offset) next = start;
            else if (end > (long)offset + viewport) next = end - viewport;
        }
        else if (start > offset) next = start;
        else if (end < (long)offset + viewport) next = end - viewport;
        return (int)Math.Clamp(next, 0, Math.Max(0L, (long)extent - viewport));
    }
}
