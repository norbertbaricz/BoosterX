using System.Drawing;

namespace BoosterX.App.Controls;

/// <summary>Easy-to-tune colors, logical pixel dimensions and UI limits. Dimensions scale with DPI.</summary>
internal static class UiPalette
{
    internal static readonly Color Background = Color.FromArgb(16, 17, 21);
    internal static readonly Color Surface = Color.FromArgb(22, 24, 29);
    internal static readonly Color Card = Color.FromArgb(30, 33, 39);
    internal static readonly Color ButtonSurface = Color.FromArgb(40, 45, 56);
    internal static readonly Color Hover = Color.FromArgb(50, 57, 70);
    internal static readonly Color Border = Color.FromArgb(38, 42, 50);
    internal static readonly Color Text = Color.FromArgb(245, 246, 248);
    internal static readonly Color Muted = Color.FromArgb(172, 181, 197);
    internal static readonly Color DisabledText = Color.FromArgb(166, 177, 195);
    internal static readonly Color DisabledSurface = Color.FromArgb(25, 27, 33);
    internal static readonly Color Accent = Color.FromArgb(190, 40, 60);
    internal static readonly Color AccentHover = Color.FromArgb(170, 34, 52);
    internal static readonly Color AccentPressed = Color.FromArgb(143, 27, 44);
    internal static readonly Color Selection = Color.FromArgb(54, 31, 41);
    internal static readonly Color ProgressTrack = Color.FromArgb(36, 39, 47);
    internal static readonly Color Focus = Color.FromArgb(229, 186, 194);
    internal static readonly Color Success = Color.FromArgb(134, 222, 174);
    internal static readonly Color Warning = Color.FromArgb(246, 204, 126);
    internal static readonly Color Error = Color.FromArgb(255, 151, 166);
    internal static readonly Color ScrollThumb = Color.FromArgb(92, 101, 116);

    internal const int ControlRadius = 8;
    internal const int ActionGap = 8;
    internal const int SectionGap = 12;
    internal const int DetailStackWidth = 760;
    internal const int DetailDescriptionMinimum = 280;
    internal const int DetailSectionGap = 20;
    internal const int MaintenanceWheelStep = 16;
    internal const int FieldHeight = 40;
    internal const int PanelRadius = 10;
    internal const int ButtonMinimumHeight = 40;
    internal const int ButtonTextSafety = 4;
    internal const int ActionIconSize = 18;
    internal const int ActionIconGap = 9;
    internal const int MaximumTextMeasureSize = 32767;
    internal const int FocusCueWidth = 20;
    internal const int FocusCueHeight = 2;
    internal const int FocusCueInset = 4;
    internal const int CaptionGlyphSize = 10;
    internal const int ProgressHeight = 6;
    internal const int ScrollBarWidth = 10;
    internal const int ScrollBarGap = 4;
    internal const int MinimumThumb = 28;
    internal const int WheelDelta = 120;
    internal const int PageWheelStep = 32;
    internal const int HorizontalWheelStep = 24;
    internal const int ScrollMeasurePasses = 4;
    internal const int DropDownMinimumHeight = 40;
    internal const int DropDownItemHeight = 32;
    internal const int DropDownItemVerticalPadding = 12;
    internal const int DropDownArrowWidth = 34;
    internal const int DropDownPadding = 6;
    internal const int DropDownMaxVisibleItems = 8;
    internal const int DropDownMinimumWidth = 180;
    internal const int DropDownMaximumWidth = 720;
    internal const int DropDownTypeSearchMilliseconds = 900;
    internal const int MaximumActivityEntries = 2000;
    internal const int MaximumActivityRows = 800;
    internal const int ResizeBorder = 7;
    internal const int CaptionButtonWidth = 40;
    internal const int AnimationIntervalMilliseconds = 30;
    internal const int UiCheckSettleMilliseconds = 100;

    internal static double Contrast(Color a, Color b)
    {
        static double Linear(byte c) => c / 255d <= 0.04045 ? c / 255d / 12.92 : Math.Pow((c / 255d + 0.055) / 1.055, 2.4);
        static double Luminance(Color c) => 0.2126 * Linear(c.R) + 0.7152 * Linear(c.G) + 0.0722 * Linear(c.B);
        double first = Luminance(a), second = Luminance(b);
        return (Math.Max(first, second) + 0.05) / (Math.Min(first, second) + 0.05);
    }
}
