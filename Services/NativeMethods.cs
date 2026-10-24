using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;

namespace BoosterX.App.Services;

internal static class NativeMethods
{
    [DllImport("powrprof.dll")] internal static extern uint PowerGetActiveScheme(IntPtr userRoot, out IntPtr guid);
    [DllImport("powrprof.dll")] internal static extern uint PowerSetActiveScheme(IntPtr userRoot, ref Guid guid);
    [DllImport("powrprof.dll")] internal static extern uint PowerEnumerate(IntPtr root, IntPtr scheme, IntPtr subgroup,
        uint accessFlags, uint index, out Guid buffer, ref uint bufferSize);
    [DllImport("kernel32.dll")] internal static extern IntPtr LocalFree(IntPtr memory);
    [DllImport("kernel32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool GlobalMemoryStatusEx(ref MemoryStatus status);
    [DllImport("kernel32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool GetSystemPowerStatus(out PowerStatus status);
    [DllImport("dnsapi.dll")] [return: MarshalAs(UnmanagedType.Bool)] internal static extern bool DnsFlushResolverCache();
    [DllImport("iphlpapi.dll")] internal static extern uint GetInterfaceDnsSettings(Guid adapter, ref DnsInterfaceSettings settings);
    [DllImport("iphlpapi.dll")] internal static extern void FreeInterfaceDnsSettings(ref DnsInterfaceSettings settings);
    [DllImport("kernel32.dll")] internal static extern uint GetOEMCP();

    [StructLayout(LayoutKind.Sequential)]
    internal struct MemoryStatus
    {
        public uint Length, Load;
        public ulong TotalPhysical, AvailablePhysical, TotalPageFile, AvailablePageFile, TotalVirtual, AvailableVirtual, AvailableExtendedVirtual;
    }
    [StructLayout(LayoutKind.Sequential)]
    internal struct PowerStatus
    {
        public byte ACLineStatus, BatteryFlag, BatteryLifePercent, SystemStatusFlag;
        public int BatteryLifeTime, BatteryFullLifeTime;
    }
    [StructLayout(LayoutKind.Sequential)]
    internal struct DnsInterfaceSettings
    {
        public uint Version;
        public ulong Flags;
        public IntPtr Domain, NameServer, SearchList;
        public uint RegistrationEnabled, RegisterAdapterName, EnableLLMNR, QueryAdapterName;
        public IntPtr ProfileNameServer;
    }

    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    internal static extern ServiceHandle OpenSCManager(string? machine, string? database, uint access);
    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    internal static extern ServiceHandle OpenService(ServiceHandle manager, string name, uint access);
    [DllImport("advapi32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool CloseServiceHandle(IntPtr service);
    [DllImport("advapi32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool QueryServiceStatus(ServiceHandle service, out ServiceStatus status);
    [DllImport("advapi32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool ControlService(ServiceHandle service, uint control, out ServiceStatus status);
    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool StartService(ServiceHandle service, uint argc, IntPtr argv);
    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool ChangeServiceConfig(ServiceHandle service, uint type, uint start, uint error,
        string? path, string? group, IntPtr tag, string? dependencies, string? account, string? password, string? display);
    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool ChangeServiceConfig2(ServiceHandle service, uint level, ref DelayedStart info);
    [StructLayout(LayoutKind.Sequential)] internal struct DelayedStart { public int Enabled; }
    [StructLayout(LayoutKind.Sequential)]
    internal struct ServiceStatus
    { public uint Type, State, ControlsAccepted, Win32ExitCode, ServiceExitCode, CheckPoint, WaitHint; }

    internal sealed class ServiceHandle : SafeHandleZeroOrMinusOneIsInvalid
    {
        public ServiceHandle() : base(true) { }
        protected override bool ReleaseHandle() => CloseServiceHandle(handle);
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    internal static extern SafeFileHandle CreateFile(string path, uint access, uint share, IntPtr security,
        uint creation, uint flags, IntPtr template);
    [DllImport("kernel32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool GetFileInformationByHandle(SafeFileHandle handle, out FileInformation info);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    internal static extern uint GetFinalPathNameByHandle(SafeFileHandle handle, StringBuilder path, uint length, uint flags);
    [DllImport("kernel32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool SetFileInformationByHandle(SafeFileHandle handle, int infoClass, ref FileDisposition info, uint size);
    [StructLayout(LayoutKind.Sequential)] internal struct FileDisposition { public byte DeleteFile; }
    [StructLayout(LayoutKind.Sequential)]
    internal struct FileInformation
    {
        public uint Attributes;
        public System.Runtime.InteropServices.ComTypes.FILETIME Created, Accessed, Written;
        public uint VolumeSerial, SizeHigh, SizeLow, Links, IndexHigh, IndexLow;
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    internal static extern int SHEmptyRecycleBin(IntPtr window, string? root, uint flags);
    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    internal static extern int SHQueryRecycleBin(string? root, ref RecycleInfo info);
    [StructLayout(LayoutKind.Sequential, Pack = 8)]
    internal struct RecycleInfo { public uint Size; public long Bytes, Items; }

    [DllImport("ole32.dll")] internal static extern int CoInitializeSecurity(IntPtr descriptor, int authCount, IntPtr authServices,
        IntPtr reserved, uint authLevel, uint impersonationLevel, IntPtr authList, uint capabilities, IntPtr reserved3);
    [DllImport("ole32.dll")] internal static extern int CoInitializeEx(IntPtr reserved, uint mode);
    [DllImport("ole32.dll")] internal static extern void CoUninitialize();
    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool ConvertStringSecurityDescriptorToSecurityDescriptor(string sddl, uint revision,
        out IntPtr descriptor, out uint size);
    [DllImport("advapi32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool MakeAbsoluteSD(IntPtr relative, IntPtr absolute, ref uint absoluteSize,
        IntPtr dacl, ref uint daclSize, IntPtr sacl, ref uint saclSize, IntPtr owner, ref uint ownerSize,
        IntPtr group, ref uint groupSize);
    [UnmanagedFunctionPointer(CallingConvention.Winapi, CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal delegate bool SetRestorePoint(ref RestorePointInfo info, out RestoreStatus status);
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    internal struct RestorePointInfo
    {
        public uint EventType, RestorePointType;
        public long Sequence;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)] public string Description;
    }
    [StructLayout(LayoutKind.Sequential)] internal struct RestoreStatus { public uint Status; public long Sequence; }
}
