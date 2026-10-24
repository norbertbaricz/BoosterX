using System.ComponentModel;
using System.Runtime.InteropServices;

namespace BoosterX.App.Controls;

internal readonly record struct WheelInput(int Delta, bool Horizontal, int Lines)
{
    internal static WheelInput From(MouseEventArgs e) => new(e.Delta, (Control.ModifierKeys & Keys.Shift) != 0, SystemInformation.MouseWheelScrollLines);
}
internal interface IScrollWheelTarget { bool ScrollWheel(WheelInput input); }

/// <summary>One UI-thread filter routes wheel input under the pointer, then to the nearest scrollable parent at an edge.</summary>
internal sealed class ScrollWheelRouter : IMessageFilter
{
    [ThreadStatic] private static ScrollWheelRouter? current;
    private int users;

    internal static void Watch(Control owner)
    {
        ScrollWheelRouter? lease = null;
        owner.HandleCreated += (_, _) =>
        {
            if (lease != null || LicenseManager.UsageMode == LicenseUsageMode.Designtime) return;
            lease = current ??= new ScrollWheelRouter();
            if (lease.users++ == 0) Application.AddMessageFilter(lease);
        };
        void Release()
        {
            if (lease == null) return;
            if (--lease.users == 0) { Application.RemoveMessageFilter(lease); if (current == lease) current = null; }
            lease = null;
        }
        owner.HandleDestroyed += (_, _) => Release();
        owner.Disposed += (_, _) => Release();
    }
    internal static bool Dispatch(Control start, WheelInput input)
    {
        if (start is DarkScrollBar bar && bar.Parent is IScrollWheelTarget)
        {
            input = input with { Horizontal = input.Horizontal || bar.Orientation == Orientation.Horizontal };
            start = bar.Parent;
        }
        for (Control? control = start; control != null; control = control.Parent)
            if (control is IScrollWheelTarget target && control.Visible && control.Enabled && target.ScrollWheel(input)) return true;
        return false;
    }
    public bool PreFilterMessage(ref Message m)
    {
        if (m.Msg is not (0x020A or 0x020E) || (Control.ModifierKeys & Keys.Control) != 0 || (m.WParam.ToInt64() & 0x0008) != 0) return false;
        long position = m.LParam.ToInt64();
        var point = new NativePoint { X = unchecked((short)(position & 0xffff)), Y = unchecked((short)((position >> 16) & 0xffff)) };
        Control? hovered = Control.FromChildHandle(WindowFromPoint(point));
        if (hovered == null || hovered.IsDisposed) return false;
        int delta = unchecked((short)((m.WParam.ToInt64() >> 16) & 0xffff));
        bool horizontal = m.Msg == 0x020E || (m.WParam.ToInt64() & 0x0004) != 0;
        if (m.Msg == 0x020E) delta = -delta; // Native horizontal wheel uses positive values toward the right.
        return Dispatch(hovered, new(delta, horizontal, SystemInformation.MouseWheelScrollLines));
    }
    [StructLayout(LayoutKind.Sequential)] private struct NativePoint { internal int X, Y; }
    [DllImport("user32.dll")] private static extern nint WindowFromPoint(NativePoint point);
}
