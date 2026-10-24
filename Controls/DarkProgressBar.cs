using System.ComponentModel;
using System.Drawing.Drawing2D;

namespace BoosterX.App.Controls;

/// <summary>A six-logical-pixel progress control, independent of the native progress class and system theme.</summary>
public class DarkProgressBar : Control
{
    private readonly System.Windows.Forms.Timer animation = new();
    private int minimum, maximum = 100, value, phase;
    private ProgressBarStyle style;
    private int speed = UiPalette.AnimationIntervalMilliseconds;

    [DefaultValue(0)] public int Minimum { get => minimum; set { minimum = Math.Max(0, value); if (maximum <= minimum) maximum = minimum + 1; this.value = Math.Clamp(this.value, minimum, maximum); Invalidate(); } }
    [DefaultValue(100)] public int Maximum { get => maximum; set { maximum = Math.Max(minimum + 1, value); this.value = Math.Clamp(this.value, minimum, maximum); Invalidate(); } }
    [DefaultValue(0)] public int Value { get => value; set { this.value = Math.Clamp(value, minimum, maximum); Invalidate(); } }
    [DefaultValue(ProgressBarStyle.Continuous)] public ProgressBarStyle Style { get => style; set { style = value; phase = 0; UpdateAnimation(); Invalidate(); } }
    [DefaultValue(UiPalette.AnimationIntervalMilliseconds)] public int MarqueeAnimationSpeed { get => speed; set { speed = Math.Clamp(value, 15, 1000); animation.Interval = speed; } }

    public DarkProgressBar()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        TabStop = false;
        AccessibleRole = AccessibleRole.ProgressBar;
        animation.Interval = speed;
        animation.Tick += (_, _) => { phase = (phase + 3) % 140; Invalidate(); };
        Size = new Size(200, UiPalette.ProgressHeight);
    }

    protected override void SetBoundsCore(int x, int y, int width, int height, BoundsSpecified specified)
        => base.SetBoundsCore(x, y, width, LogicalToDeviceUnits(UiPalette.ProgressHeight), specified);
    public override Size GetPreferredSize(Size proposedSize) => new(proposedSize.Width > 0 ? proposedSize.Width : 200, LogicalToDeviceUnits(UiPalette.ProgressHeight));
    protected override void OnPaintBackground(PaintEventArgs e)
    {
        using var brush = new SolidBrush(Parent?.BackColor ?? UiPalette.Background);
        e.Graphics.FillRectangle(brush, ClientRectangle);
    }
    protected override void OnPaint(PaintEventArgs e)
    {
        if (Width < 2 || Height < 2) return;
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using var track = UiDrawing.Round(new RectangleF(0, 0, Width, Height), Height / 2f);
        using var background = new SolidBrush(UiPalette.ProgressTrack);
        e.Graphics.FillPath(background, track);
        var saved = e.Graphics.Save();
        try
        {
            e.Graphics.SetClip(track, CombineMode.Intersect);
            using var fill = new SolidBrush(UiPalette.Accent);
            if (style == ProgressBarStyle.Marquee)
            {
                float span = Math.Max(Height * 3, Width * 0.24f), left = (Width + span) * phase / 140f - span;
                using var segment = UiDrawing.Round(new RectangleF(left, 0, span, Height), Height / 2f);
                e.Graphics.FillPath(fill, segment);
            }
            else
            {
                float width = Width * (value - minimum) / (float)(maximum - minimum);
                if (width > 0) e.Graphics.FillRectangle(fill, 0, 0, width, Height);
            }
        }
        finally { e.Graphics.Restore(saved); }
    }
    private void UpdateAnimation() => animation.Enabled = IsHandleCreated && Visible && style == ProgressBarStyle.Marquee && !DesignMode && !Disposing;
    protected override void OnHandleCreated(EventArgs e) { base.OnHandleCreated(e); UpdateAnimation(); }
    protected override void OnHandleDestroyed(EventArgs e) { animation.Stop(); base.OnHandleDestroyed(e); }
    protected override void OnVisibleChanged(EventArgs e) { base.OnVisibleChanged(e); UpdateAnimation(); }
    protected override void OnDpiChangedAfterParent(EventArgs e) { base.OnDpiChangedAfterParent(e); Height = LogicalToDeviceUnits(UiPalette.ProgressHeight); }
    protected override void Dispose(bool disposing) { if (disposing) animation.Dispose(); base.Dispose(disposing); }
}
