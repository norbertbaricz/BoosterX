using System.ComponentModel;
using System.Runtime.InteropServices;

namespace BoosterX.App.Controls;

/// <summary>Native text selection/copy with application-painted scrollbars instead of Windows scrollbars.</summary>
public sealed class DarkTextView : UserControl, IScrollWheelTarget
{
    private readonly WheelRichTextBox editor = new() { BorderStyle = BorderStyle.None, ReadOnly = true, DetectUrls = false, ScrollBars = RichTextBoxScrollBars.None };
    private readonly DarkScrollBar vertical = new();
    private readonly DarkScrollBar horizontal = new() { Orientation = Orientation.Horizontal };
    private WheelAccumulator wheel;
    private int visibleLines = 1;
    private bool updating, scheduled, showVertical, showHorizontal;
    private int horizontalExtent;
    private bool extentDirty = true;
    private RichTextBoxScrollBars allowed = RichTextBoxScrollBars.Both;

    [DefaultValue(""), System.Diagnostics.CodeAnalysis.AllowNull] public override string Text { get => editor?.Text ?? base.Text; set { if (editor == null) { base.Text = value ?? ""; return; } editor.Text = value ?? ""; extentDirty = true; ScheduleUpdate(); } }
    [DefaultValue(true)] public bool ReadOnly { get => editor.ReadOnly; set => editor.ReadOnly = value; }
    [DefaultValue(true)] public bool WordWrap { get => editor.WordWrap; set { editor.WordWrap = value; extentDirty = true; ScheduleUpdate(); } }
    [DefaultValue(false)] public bool DetectUrls { get => editor.DetectUrls; set => editor.DetectUrls = value; }
    [DefaultValue(RichTextBoxScrollBars.Both)] public RichTextBoxScrollBars ScrollBars { get => allowed; set { allowed = value; ScheduleUpdate(); } }
    [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)] public int TextLength => editor.TextLength;
    [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)] public int SelectionStart { get => editor.SelectionStart; set => editor.SelectionStart = value; }
    [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)] public string SelectedText { get => editor.SelectedText; set => editor.SelectedText = value; }
    internal RichTextBox Editor => editor;

    public DarkTextView()
    {
        SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        ScrollWheelRouter.Watch(this);
        BackColor = UiPalette.Surface; ForeColor = UiPalette.Text;
        editor.BackColor = BackColor; editor.ForeColor = ForeColor;
        vertical.Visible = false; horizontal.Visible = false;
        Controls.Add(editor); Controls.Add(vertical); Controls.Add(horizontal);
        editor.TextChanged += (_, _) => { extentDirty = true; ScheduleUpdate(); };
        editor.VScroll += (_, _) => ScheduleUpdate(); editor.HScroll += (_, _) => ScheduleUpdate();
        editor.MouseWheel += (_, _) => ScheduleUpdate(); editor.KeyUp += (_, _) => ScheduleUpdate();
        editor.SelectionChanged += (_, _) => ScheduleUpdate();
        vertical.ValueChanged += (_, _) =>
        {
            if (updating || !editor.IsHandleCreated) return;
            int current = (int)Native.SendMessage(editor.Handle, 0x00CE, 0, 0);
            Native.SendMessage(editor.Handle, 0x00B6, 0, vertical.Value - current);
            ScheduleUpdate();
        };
        horizontal.ValueChanged += (_, _) =>
        {
            if (updating || !editor.IsHandleCreated) return;
            var point = new Native.NativePoint(); Native.SendPoint(editor.Handle, 0x04DD, 0, ref point);
            point.X = horizontal.Value; Native.SendPoint(editor.Handle, 0x04DE, 0, ref point);
            ScheduleUpdate();
        };
    }
    public void AppendText(string text) { editor.AppendText(text); extentDirty = true; ScheduleUpdate(); }
    public void Select(int start, int length) => editor.Select(start, length);
    public void ScrollToCaret() { editor.ScrollToCaret(); ScheduleUpdate(); }
    public void Copy() => editor.Copy();
    bool IScrollWheelTarget.ScrollWheel(WheelInput input)
    {
        if (!editor.IsHandleCreated || !Visible) return false;
        bool across = input.Horizontal || !vertical.Visible && horizontal.Visible;
        DarkScrollBar bar = across ? horizontal : vertical;
        WheelMove move = wheel.Move(bar.Value, bar.Maximum, input.Delta,
            across ? LogicalToDeviceUnits(UiPalette.HorizontalWheelStep) : 1,
            across ? editor.ClientSize.Width : visibleLines, input.Lines, across);
        if (move.Accepted && move.Value != bar.Value)
        {
            int before = NativeOffset(across);
            bar.Value = move.Value;
            // RichEdit can clamp a request sooner than its measured line range. Yield to the
            // surrounding page if the native text did not actually move at that edge.
            if (NativeOffset(across) == before) return false;
        }
        return move.Accepted;
    }
    private int NativeOffset(bool horizontalAxis)
    {
        if (!horizontalAxis) return (int)Native.SendMessage(editor.Handle, 0x00CE, 0, 0);
        var point = new Native.NativePoint(); Native.SendPoint(editor.Handle, 0x04DD, 0, ref point);
        return point.X;
    }
    protected override void OnFontChanged(EventArgs e) { base.OnFontChanged(e); if (editor != null) editor.Font = Font; extentDirty = true; ScheduleUpdate(); }
    protected override void OnForeColorChanged(EventArgs e) { base.OnForeColorChanged(e); if (editor != null) editor.ForeColor = ForeColor; }
    protected override void OnBackColorChanged(EventArgs e) { base.OnBackColorChanged(e); if (editor != null) editor.BackColor = BackColor; }
    protected override void OnHandleCreated(EventArgs e) { base.OnHandleCreated(e); ScheduleUpdate(); }
    protected override void OnDpiChangedAfterParent(EventArgs e) { base.OnDpiChangedAfterParent(e); extentDirty = true; ScheduleUpdate(); }
    protected override void OnLayout(LayoutEventArgs e) { base.OnLayout(e); if (IsHandleCreated) UpdateScrollbars(); }
    protected override void OnSizeChanged(EventArgs e) { base.OnSizeChanged(e); ScheduleUpdate(); }
    protected override void OnVisibleChanged(EventArgs e) { base.OnVisibleChanged(e); if (Visible) ScheduleUpdate(); }
    private void ScheduleUpdate()
    {
        if (updating || !IsHandleCreated || IsDisposed || Disposing || scheduled) return;
        scheduled = true;
        BeginInvoke((Action)(() => { scheduled = false; if (!IsDisposed && !Disposing) UpdateScrollbars(); }));
    }
    private void UpdateScrollbars()
    {
        if (updating || Width < 1 || Height < 1 || !Visible) return;
        updating = true;
        try
        {
            int bar = LogicalToDeviceUnits(UiPalette.ScrollBarWidth), gap = LogicalToDeviceUnits(UiPalette.ScrollBarGap);
            if (extentDirty)
            {
                horizontalExtent = 0;
                if (!WordWrap)
                    foreach (string line in editor.Lines)
                        horizontalExtent = Math.Max(horizontalExtent, TextRenderer.MeasureText(line, Font, Size.Empty,
                            TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix | TextFormatFlags.ExpandTabs).Width + LogicalToDeviceUnits(12));
                extentDirty = false;
            }
            bool allowHorizontal = !WordWrap && allowed is (RichTextBoxScrollBars.Both or RichTextBoxScrollBars.Horizontal or RichTextBoxScrollBars.ForcedBoth or RichTextBoxScrollBars.ForcedHorizontal);
            bool needH = false, needV = false;
            int firstBefore = editor.IsHandleCreated ? Math.Max(0, (int)Native.SendMessage(editor.Handle, 0x00CE, 0, 0)) : 0;
            int anchor = editor.IsHandleCreated ? Math.Max(0, editor.GetFirstCharIndexFromLine(firstBefore)) : 0;
            for (int pass = 0; pass < UiPalette.ScrollMeasurePasses; pass++)
            {
                int width = Math.Max(1, Width - (needV ? bar + gap : 0)), height = Math.Max(1, Height - (needH ? bar + gap : 0));
                editor.Bounds = new Rectangle(0, 0, width, height);
                if (!editor.IsHandleCreated) editor.CreateControl();
                int lines = Math.Max(1, editor.GetLineFromCharIndex(editor.TextLength) + 1);
                int first = Math.Max(0, (int)Native.SendMessage(editor.Handle, 0x00CE, 0, 0));
                int firstChar = editor.GetFirstCharIndexFromLine(first), nextChar = editor.GetFirstCharIndexFromLine(first + 1);
                int lineHeight = firstChar >= 0 && nextChar >= 0
                    ? editor.GetPositionFromCharIndex(nextChar).Y - editor.GetPositionFromCharIndex(firstChar).Y : Font.Height;
                if (lineHeight < 1) lineHeight = Math.Max(1, Font.Height);
                var formatting = new Native.NativeRect(); Native.SendRect(editor.Handle, 0x00B2, 0, ref formatting);
                int textHeight = formatting.Bottom > formatting.Top ? formatting.Bottom - formatting.Top : height;
                int visible = Math.Max(1, textHeight / lineHeight);
                visibleLines = visible;
                bool measuredV = allowed is not (RichTextBoxScrollBars.None or RichTextBoxScrollBars.Horizontal or RichTextBoxScrollBars.ForcedHorizontal) && lines > visible;
                bool measuredH = allowHorizontal && horizontalExtent > width;
                vertical.SmallChange = 1;
                horizontal.SmallChange = LogicalToDeviceUnits(UiPalette.HorizontalWheelStep);
                vertical.Configure(visible, lines, first);
                var point = new Native.NativePoint(); Native.SendPoint(editor.Handle, 0x04DD, 0, ref point);
                horizontal.Configure(width, horizontalExtent, point.X);
                if (measuredV == needV && measuredH == needH) break;
                needV = measuredV; needH = measuredH;
            }
            showVertical = needV; showHorizontal = needH;
            vertical.Visible = needV; horizontal.Visible = needH;
            vertical.Bounds = new Rectangle(Math.Max(0, Width - bar), 0, bar, Math.Max(1, Height - (needH ? bar + gap : 0)));
            horizontal.Bounds = new Rectangle(0, Math.Max(0, Height - bar), Math.Max(1, Width - (needV ? bar + gap : 0)), bar);
            // Preserve the visible text anchor across bar-width/word-wrap reflow, without selecting text.
            int target = editor.GetLineFromCharIndex(Math.Min(anchor, editor.TextLength));
            int current = (int)Native.SendMessage(editor.Handle, 0x00CE, 0, 0);
            if (target != current) Native.SendMessage(editor.Handle, 0x00B6, 0, target - current);
            vertical.Configure(visibleLines, Math.Max(1, editor.GetLineFromCharIndex(editor.TextLength) + 1),
                Math.Max(0, (int)Native.SendMessage(editor.Handle, 0x00CE, 0, 0)));
        }
        finally { updating = false; }
    }
    private static class Native
    {
        [StructLayout(LayoutKind.Sequential)] internal struct NativePoint { public int X, Y; }
        [StructLayout(LayoutKind.Sequential)] internal struct NativeRect { public int Left, Top, Right, Bottom; }
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] internal static extern nint SendMessage(nint window, uint message, nint wParam, nint lParam);
        [DllImport("user32.dll", EntryPoint = "SendMessageW")] internal static extern nint SendPoint(nint window, uint message, nint wParam, ref NativePoint point);
        [DllImport("user32.dll", EntryPoint = "SendMessageW")] internal static extern nint SendRect(nint window, uint message, nint wParam, ref NativeRect rectangle);
    }
    private sealed class WheelRichTextBox : RichTextBox
    {
        protected override void OnMouseWheel(MouseEventArgs e)
        {
            bool consumed = ScrollWheelRouter.Dispatch(this, WheelInput.From(e));
            if (e is HandledMouseEventArgs handled) handled.Handled = consumed;
            if (!consumed) base.OnMouseWheel(e);
        }
    }
}
