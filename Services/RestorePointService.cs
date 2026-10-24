using System.ComponentModel;
using System.Runtime.InteropServices;

namespace BoosterX.App.Services;

public static class RestorePointService
{
    public static string TryCreate()
    {
        if (ComSecurityScope.SecurityResult < 0 && ComSecurityScope.SecurityResult != unchecked((int)0x80010119))
            return $"Windows restore point unavailable: COM security initialization returned 0x{ComSecurityScope.SecurityResult:X8}. Setting backups remain available.";
        int comResult = NativeMethods.CoInitializeEx(IntPtr.Zero, 0); // The worker thread needs its own COM initialization.
        if (comResult < 0 && comResult != unchecked((int)0x80010106))
            return $"Windows restore point unavailable: COM thread initialization returned 0x{comResult:X8}. Setting backups remain available.";
        IntPtr library = IntPtr.Zero;
        try
        {
            // Load the optional DLL dynamically; computers without System Restore can still run BoosterX.
            library = NativeLibrary.Load(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "srclient.dll"));
            var create = Marshal.GetDelegateForFunctionPointer<NativeMethods.SetRestorePoint>(NativeLibrary.GetExport(library, "SRSetRestorePointW"));
            var begin = new NativeMethods.RestorePointInfo { EventType = 100, RestorePointType = 12, Description = "BoosterX before changes" };
            if (!create(ref begin, out var state)) return "Windows restore point unavailable: " + new Win32Exception((int)state.Status).Message + ". Setting backups remain available.";
            var end = new NativeMethods.RestorePointInfo { EventType = 101, RestorePointType = 12, Sequence = state.Sequence, Description = "BoosterX before changes" };
            if (!create(ref end, out var finished)) return "Windows restore-point finalization failed: " + new Win32Exception((int)finished.Status).Message + ". Setting backups remain available.";
            return state.Sequence == 0
                ? "Windows did not report a new restore point; creation may be throttled. Setting backups remain available."
                : "Windows accepted restore-point request " + state.Sequence + ". Windows may reuse/skip a recent point. This does not back up deleted personal files or app data.";
        }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException or Win32Exception)
        { return "Windows restore point unavailable: " + ex.Message + ". Setting backups remain available."; }
        finally
        {
            if (library != IntPtr.Zero) NativeLibrary.Free(library);
            if (comResult >= 0) NativeMethods.CoUninitialize();
        }
    }
}
