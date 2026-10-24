using System.ComponentModel;
using System.Runtime.InteropServices;

namespace BoosterX.App.Controls;

/// <summary>A real borderless window. No non-client frame extension, rounded Region or native-caption style hacks.</summary>
public class DarkForm : Form
{
    private ResizeEdges resizeEdges;
    private Point resizeStart;
    private Rectangle resizeBounds;
    private bool InDesigner => DesignMode || LicenseManager.UsageMode == LicenseUsageMode.Designtime;
    public DarkForm()
    {
        FormBorderStyle = FormBorderStyle.None;
        BackColor = UiPalette.Background;
        DoubleBuffered = true;
        SetStyle(ControlStyles.ResizeRedraw, true);
    }
    protected override CreateParams CreateParams
    {
        get
        {
            CreateParams value = base.CreateParams;
            if (!InDesigner)
            {
                value.Style &= ~0x00CF0000; // Caption, sizing frame, system menu and native caption buttons must stay absent.
                value.ExStyle &= ~(0x00000001 | 0x00000200 | 0x00020000); // No classic dialog/client/static edge.
            }
            return value;
        }
    }
    protected void AttachTitleDrag(params Control[] controls)
    { foreach (Control control in controls) control.MouseDown += TitleMouseDown; }
    private void TitleMouseDown(object? sender, MouseEventArgs e)
    {
        if (InDesigner) return;
        if (e.Button == MouseButtons.Right) { ShowWindowMenu(Cursor.Position); return; }
        if (e.Button != MouseButtons.Left) return;
        if (e.Clicks >= 2) { if (MaximizeBox) ToggleMaximize(); return; }
        Native.ReleaseCapture(); Native.SendMessage(Handle, 0x00A1, 2, 0);
    }
    protected void ToggleMaximize()
    {
        MaximizedBounds = Screen.FromControl(this).WorkingArea;
        WindowState = WindowState == FormWindowState.Maximized ? FormWindowState.Normal : FormWindowState.Maximized;
    }
    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        if (!InDesigner && OperatingSystem.IsWindowsVersionAtLeast(10, 0, 22000))
        {
            int dark = 1, round = 2;
            _ = Native.DwmSetWindowAttribute(Handle, 20, ref dark, 4);
            _ = Native.DwmSetWindowAttribute(Handle, 33, ref round, 4);
        }
    }
    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        if (WindowState != FormWindowState.Normal || Width < 2 || Height < 2) return;
        using var border = new Pen(UiPalette.Border);
        e.Graphics.DrawRectangle(border, 0, 0, Width - 1, Height - 1);
    }
    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (InDesigner || e.Button != MouseButtons.Left || WindowState != FormWindowState.Normal) return;
        resizeEdges = WindowGeometry.Hit(e.Location, ClientSize, LogicalToDeviceUnits(UiPalette.ResizeBorder));
        if (resizeEdges == ResizeEdges.None) return;
        resizeStart = Cursor.Position; resizeBounds = Bounds; Capture = true;
    }
    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (InDesigner || WindowState != FormWindowState.Normal) return;
        ResizeEdges edges = resizeEdges != ResizeEdges.None ? resizeEdges : WindowGeometry.Hit(e.Location, ClientSize, LogicalToDeviceUnits(UiPalette.ResizeBorder));
        Cursor = edges is ResizeEdges.Left or ResizeEdges.Right ? Cursors.SizeWE : edges is ResizeEdges.Top or ResizeEdges.Bottom ? Cursors.SizeNS
            : edges == (ResizeEdges.Left | ResizeEdges.Top) || edges == (ResizeEdges.Right | ResizeEdges.Bottom) ? Cursors.SizeNWSE
            : edges != ResizeEdges.None ? Cursors.SizeNESW : Cursors.Default;
        if (resizeEdges != ResizeEdges.None && Capture)
            Bounds = WindowGeometry.Resize(resizeBounds, new Point(Cursor.Position.X - resizeStart.X, Cursor.Position.Y - resizeStart.Y), resizeEdges, MinimumSize);
    }
    protected override void OnMouseUp(MouseEventArgs e) { resizeEdges = ResizeEdges.None; Capture = false; base.OnMouseUp(e); }
    protected override void OnMouseCaptureChanged(EventArgs e) { if (!Capture) resizeEdges = ResizeEdges.None; base.OnMouseCaptureChanged(e); }
    protected override void OnMouseLeave(EventArgs e) { if (!Capture) Cursor = Cursors.Default; base.OnMouseLeave(e); }
    protected override void WndProc(ref Message m)
    {
        if (!InDesigner && m.Msg == 0x0104 && m.WParam.ToInt32() == (int)Keys.Space)
        { ShowWindowMenu(new Point(Left + LogicalToDeviceUnits(16), Top + LogicalToDeviceUnits(44))); m.Result = 0; return; }
        base.WndProc(ref m);
        if (InDesigner || m.Msg != 0x0024 || m.LParam == 0) return;
        var info = Marshal.PtrToStructure<Native.MinMaxInfo>(m.LParam);
        var bounds = new Native.MonitorInfo { Size = Marshal.SizeOf<Native.MonitorInfo>() };
        if (!Native.GetMonitorInfo(Native.MonitorFromWindow(Handle, 2), ref bounds)) return;
        info.MaxPosition = new(bounds.Work.Left - bounds.Monitor.Left, bounds.Work.Top - bounds.Monitor.Top);
        info.MaxSize = new(bounds.Work.Right - bounds.Work.Left, bounds.Work.Bottom - bounds.Work.Top);
        info.MinTrackSize = new(MinimumSize.Width, MinimumSize.Height);
        Marshal.StructureToPtr(info, m.LParam, false);
    }
    private void ShowWindowMenu(Point location)
    {
        var menu = new ContextMenuStrip { Renderer = new DarkMenuRenderer(), ShowImageMargin = true };
        UiIcons.AddMenuItem(menu.Items, "Restore", UiIcon.Restore, (_, _) => WindowState = FormWindowState.Normal).Enabled = WindowState != FormWindowState.Normal;
        UiIcons.AddMenuItem(menu.Items, "Minimize", UiIcon.Disable, (_, _) => WindowState = FormWindowState.Minimized).Enabled = MinimizeBox;
        UiIcons.AddMenuItem(menu.Items, "Maximize", UiIcon.Apps, (_, _) => ToggleMaximize()).Enabled = MaximizeBox && WindowState != FormWindowState.Maximized;
        menu.Items.Add(new ToolStripSeparator()); UiIcons.AddMenuItem(menu.Items, "Close", UiIcon.Error, (_, _) => Close());
        menu.Closed += (_, _) => menu.Dispose(); menu.Show(location);
    }
    private static class Native
    {
        [StructLayout(LayoutKind.Sequential)] internal struct NativePoint { public int X, Y; public NativePoint(int x, int y) { X = x; Y = y; } }
        [StructLayout(LayoutKind.Sequential)] internal struct MinMaxInfo { public NativePoint Reserved, MaxSize, MaxPosition, MinTrackSize, MaxTrackSize; }
        [StructLayout(LayoutKind.Sequential)] internal struct Rect { public int Left, Top, Right, Bottom; }
        [StructLayout(LayoutKind.Sequential)] internal struct MonitorInfo { public int Size; public Rect Monitor, Work; public uint Flags; }
        [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] internal static extern bool ReleaseCapture();
        [DllImport("user32.dll")] internal static extern nint SendMessage(nint window, uint message, nint wParam, nint lParam);
        [DllImport("user32.dll")] internal static extern nint MonitorFromWindow(nint window, uint flags);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] [return: MarshalAs(UnmanagedType.Bool)] internal static extern bool GetMonitorInfo(nint monitor, ref MonitorInfo info);
        [DllImport("dwmapi.dll")] internal static extern int DwmSetWindowAttribute(nint window, int attribute, ref int value, int size);
    }
}

internal sealed class DarkMenuRenderer : ToolStripProfessionalRenderer
{
    protected override void OnRenderToolStripBackground(ToolStripRenderEventArgs e)
    { using var brush = new SolidBrush(UiPalette.Card); e.Graphics.FillRectangle(brush, e.AffectedBounds); }
    protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e)
    { using var border = new Pen(UiPalette.Border); e.Graphics.DrawRectangle(border, 0, 0, e.ToolStrip.Width - 1, e.ToolStrip.Height - 1); }
    protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
    {
        using var brush = new SolidBrush(e.Item.Selected ? UiPalette.Hover : UiPalette.Card);
        e.Graphics.FillRectangle(brush, new Rectangle(Point.Empty, e.Item.Size));
        if (e.Item.Tag is UiIcon icon && e.ToolStrip is ToolStrip strip)
        {
            int side = Math.Min(strip.LogicalToDeviceUnits(16), Math.Max(1, e.Item.Height - strip.LogicalToDeviceUnits(4)));
            UiIcons.Draw(e.Graphics, new Rectangle(strip.LogicalToDeviceUnits(6), (e.Item.Height - side) / 2, side, side),
                icon, e.Item.Enabled ? UiPalette.Text : UiPalette.DisabledText);
        }
    }
    protected override void OnRenderImageMargin(ToolStripRenderEventArgs e)
    { using var brush = new SolidBrush(UiPalette.Card); e.Graphics.FillRectangle(brush, e.AffectedBounds); }
    protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
    { e.TextColor = e.Item.Enabled ? UiPalette.Text : UiPalette.DisabledText; base.OnRenderItemText(e); }
    protected override void OnRenderSeparator(ToolStripSeparatorRenderEventArgs e)
    { using var pen = new Pen(UiPalette.Border); e.Graphics.DrawLine(pen, 8, e.Item.Height / 2, e.Item.Width - 8, e.Item.Height / 2); }
}
