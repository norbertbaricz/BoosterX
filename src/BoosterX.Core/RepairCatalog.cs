namespace BoosterX.Core;

public sealed record RepairDefinition(string Id, string Title, string Category, string Description,
    string Executable, string[] Arguments, bool Servicing = false, bool ReadOnly = false, bool Restart = false);

public static class RepairCatalog
{
    public static readonly IReadOnlyList<RepairDefinition> All =
    [
        new("dism-check","Quick component-health status","Windows","Read the component store's recorded corruption status; does not run a full scan.","dism.exe",["/Online","/Cleanup-Image","/CheckHealth","/English","/NoRestart"],true,true),
        new("dism-scan","Scan component health","Windows","Full DISM component-store scan; review the findings in Activity.","dism.exe",["/Online","/Cleanup-Image","/ScanHealth","/English","/NoRestart"],true,true),
        new("dism-repair","Repair component store","Windows","DISM RestoreHealth can download repair content and take a long time; no automatic reboot.","dism.exe",["/Online","/Cleanup-Image","/RestoreHealth","/English","/NoRestart"],true),
        new("sfc-verify","Verify protected system files","Windows","Read-only SFC verification; records findings without repairing files.","sfc.exe",["/verifyonly"],true,true),
        new("sfc","Repair protected system files","Windows","Run SFC scannow. Review Windows output; completion does not prove every issue was repaired.","sfc.exe",["/scannow"],true),
        new("component-analysis","Analyze component-store space","Storage","Report component-store size and cleanup recommendation; do not manually delete WinSxS files.","dism.exe",["/Online","/Cleanup-Image","/AnalyzeComponentStore","/English","/NoRestart"],true,true),
        new("components","Clean superseded components","Storage","Run supported component cleanup without ResetBase. No BoosterX undo.","dism.exe",["/Online","/Cleanup-Image","/StartComponentCleanup","/English","/NoRestart"],true),
        new("disk-scan","Scan system disk online","Storage","Online CHKDSK scan; does not schedule an offline repair or reboot.","chkdsk.exe",["$SystemDrive","/scan"],true,true),
        new("drive-optimize","Optimize system drive using Windows","Storage","Let Windows choose the media-appropriate optimization/TRIM. Can cause substantial I/O; no BoosterX undo.","defrag.exe",["$SystemDrive","/O","/U","/V"],true),
        new("feature-list","List Windows optional features","Windows","Read optional feature states. No feature is enabled or removed.","dism.exe",["/Online","/Get-Features","/Format:Table","/English"],true,true),
        new("package-list","List Windows servicing packages","Windows","Read installed servicing package states; nothing is uninstalled.","dism.exe",["/Online","/Get-Packages","/Format:Table","/English"],true,true),
        new("network-config","Inspect IP configuration","Network","Report current IP, DNS, DHCP and gateway configuration for all adapters.","ipconfig.exe",["/all"],false,true),
        new("dns-cache-list","Inspect DNS resolver cache","Network","Read cached DNS entries; output can include recently used host names.","ipconfig.exe",["/displaydns"],false,true),
        new("winsock-list","Inspect Winsock providers","Network","Read the network provider catalog without resetting it.","netsh.exe",["winsock","show","catalog"],false,true),
        new("tcp-state","Inspect global TCP settings","Network","Read Windows TCP settings. No autotuning, MTU or congestion algorithm is changed.","netsh.exe",["interface","tcp","show","global"],false,true),
        new("ipv4-state","Inspect IPv4 adapter settings","Network","Read IPv4 interface configuration without modifying an adapter.","netsh.exe",["interface","ipv4","show","config"],false,true),
        new("wlan-state","Inspect Wi-Fi interfaces","Network","Read Wi-Fi connection details; can include local SSID/BSSID information in the session log.","netsh.exe",["wlan","show","interfaces"],false,true),
        new("winsock","Reset Winsock catalog","Network","Troubleshooting only. VPN/security providers may need repair; restart required; no BoosterX undo.","netsh.exe",["winsock","reset"],false,false,true),
        new("power-requests","Inspect sleep-blocking requests","Power","Read applications/drivers currently preventing sleep; nothing is terminated.","powercfg.exe",["/requests"],false,true),
        new("sleep-states","Inspect supported sleep states","Power","Read available sleep states and the reasons other states are unavailable.","powercfg.exe",["/a"],false,true),
        new("last-wake","Inspect last wake source","Power","Read the last recorded wake source; wake timers are preserved.","powercfg.exe",["/lastwake"],false,true),
        new("clock-status","Inspect Windows time status","Diagnostics","Read time-service status; system time is not changed.","w32tm.exe",["/query","/status"],false,true),
        new("system-events","Read recent System warnings/errors","Diagnostics","Read up to 50 recent System warning/error events; logs are preserved.","wevtutil.exe",["qe","System","/q:*[System[(Level=1 or Level=2 or Level=3)]]","/c:50","/rd:true","/f:text"],false,true)
    ];
    public static string[] ArgumentsFor(RepairDefinition definition, string systemDrive)
    {
        if (systemDrive.Length != 2 || !char.IsAsciiLetter(systemDrive[0]) || systemDrive[1] != ':') throw new InvalidDataException("Invalid system drive.");
        return definition.Arguments.Select(argument => argument == "$SystemDrive" ? systemDrive : argument).ToArray();
    }
}
