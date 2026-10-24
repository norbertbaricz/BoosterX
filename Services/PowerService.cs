using System.ComponentModel;
using System.Runtime.InteropServices;
using BoosterX.Core;

namespace BoosterX.App.Services;

public sealed record PowerSnapshot(Guid Active);

public static class PowerService
{
    public static readonly Guid HighPerformance = new("8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c");

    public static PowerSnapshot Capture()
    {
        uint code = NativeMethods.PowerGetActiveScheme(IntPtr.Zero, out IntPtr pointer);
        if (code != 0) throw new Win32Exception((int)code);
        try { return new(Marshal.PtrToStructure<Guid>(pointer)); }
        finally { NativeMethods.LocalFree(pointer); }
    }

    public static bool Exists(Guid wanted)
    {
        for (uint i = 0; i < 256; i++)
        {
            uint size = 16;
            uint result = NativeMethods.PowerEnumerate(IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, 16, i, out Guid guid, ref size);
            if (result == 259) return false;
            if (result != 0) throw new Win32Exception((int)result);
            if (guid == wanted) return true;
        }
        throw new IOException("Unexpected power plan count.");
    }

    public static void Set(Guid guid)
    {
        if (!Exists(guid)) throw new IOException("The saved power plan is no longer available.");
        uint code = NativeMethods.PowerSetActiveScheme(IntPtr.Zero, ref guid);
        if (code != 0) throw new Win32Exception((int)code);
        if (Capture().Active != guid) throw new IOException("Windows did not activate the requested power plan.");
    }

    public static StepOutcome EnableHighPerformance()
    {
        if (!NativeMethods.GetSystemPowerStatus(out var status) || status.ACLineStatus != 1)
            return new("Skipped: AC power could not be confirmed. Battery operation keeps the existing plan.", Skipped: true);
        if (!Exists(HighPerformance)) return new("Skipped: this PC does not expose a High performance plan (common with Modern Standby).", Skipped: true);
        Set(HighPerformance);
        return new("High performance plan enabled. Watch temperatures and power consumption.");
    }
}
