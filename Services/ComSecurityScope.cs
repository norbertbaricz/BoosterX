using System.ComponentModel;
using System.Runtime.InteropServices;

namespace BoosterX.App.Services;

/// <summary>Process-local COM callback permissions required by the documented System Restore API.</summary>
internal sealed class ComSecurityScope : IDisposable
{
    private readonly bool initialized;
    public static int SecurityResult { get; private set; }

    public ComSecurityScope()
    {
        int result = NativeMethods.CoInitializeEx(IntPtr.Zero, 2); // STA, consistent with the Windows Forms entry point.
        Marshal.ThrowExceptionForHR(result);
        initialized = true;
        try { SecurityResult = ConfigureSecurity(); }
        catch (Exception ex) when (ex is Win32Exception or COMException)
        { SecurityResult = ex.HResult; } // Optional System Restore must not prevent the rest of the app from starting.
    }

    private static int ConfigureSecurity()
    {
        // LocalService, NetworkService, principal self, LocalSystem and administrators; local execute rights only.
        const string sddl = "O:BAG:BAD:(A;;0x3;;;LS)(A;;0x3;;;NS)(A;;0x3;;;PS)(A;;0x3;;;SY)(A;;0x3;;;BA)";
        if (!NativeMethods.ConvertStringSecurityDescriptorToSecurityDescriptor(sddl, 1, out IntPtr relative, out _))
            throw new Win32Exception(Marshal.GetLastWin32Error());
        var allocations = new List<IntPtr>();
        try
        {
            uint absoluteSize = 0, daclSize = 0, saclSize = 0, ownerSize = 0, groupSize = 0;
            bool measured = NativeMethods.MakeAbsoluteSD(relative, IntPtr.Zero, ref absoluteSize, IntPtr.Zero, ref daclSize,
                IntPtr.Zero, ref saclSize, IntPtr.Zero, ref ownerSize, IntPtr.Zero, ref groupSize);
            if (!measured && Marshal.GetLastWin32Error() != 122) throw new Win32Exception(Marshal.GetLastWin32Error());
            IntPtr Allocate(uint size)
            {
                if (size == 0) return IntPtr.Zero;
                IntPtr pointer = Marshal.AllocHGlobal(checked((int)size)); allocations.Add(pointer); return pointer;
            }
            IntPtr absolute = Allocate(absoluteSize), dacl = Allocate(daclSize), sacl = Allocate(saclSize),
                owner = Allocate(ownerSize), group = Allocate(groupSize);
            if (!NativeMethods.MakeAbsoluteSD(relative, absolute, ref absoluteSize, dacl, ref daclSize,
                    sacl, ref saclSize, owner, ref ownerSize, group, ref groupSize)) throw new Win32Exception(Marshal.GetLastWin32Error());
            // CoInitializeSecurity requires an absolute descriptor and copies it before returning.
            return NativeMethods.CoInitializeSecurity(absolute, -1, IntPtr.Zero, IntPtr.Zero, 6, 2, IntPtr.Zero, 0, IntPtr.Zero);
        }
        finally
        {
            foreach (IntPtr allocation in allocations) Marshal.FreeHGlobal(allocation);
            NativeMethods.LocalFree(relative);
        }
    }

    public void Dispose() { if (initialized) NativeMethods.CoUninitialize(); }
}
