using System.Diagnostics;
using System.IO.Enumeration;
using System.ComponentModel;
using System.Runtime.InteropServices;
using BoosterX.Core;

namespace BoosterX.App.Services;

public sealed record CleanupCategory(string Id, string Title, string Description, bool Recommended,
    Func<IEnumerable<string>> Roots, string[]? Patterns = null, string[]? Processes = null, string[]? Services = null);
public sealed record CleanupLocation(CleanupCategory Category, string Root);

public static class CleanupCatalog
{
    public const int IdleRecheckMilliseconds = 500;
    private static string Local => Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
    private static string Roaming => Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
    private static string Windows => Environment.GetFolderPath(Environment.SpecialFolder.Windows);
    private static string Common => Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
    private static IEnumerable<string> L(params string[] paths) => paths.Select(p => Path.Combine(Local,p));
    private static IEnumerable<string> R(params string[] paths) => paths.Select(p => Path.Combine(Roaming,p));
    private static IEnumerable<string> Chromium(string relative)
    {
        string root = Path.Combine(Local,relative);
        if (!Directory.Exists(root) || TempScanner.HasReparseAncestor(root)) yield break;
        string[] profiles;
        try { profiles = Directory.GetDirectories(root).Where(p => Path.GetFileName(p) == "Default" || Path.GetFileName(p).StartsWith("Profile ",StringComparison.Ordinal)).ToArray(); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { yield break; }
        foreach (string profile in profiles)
            foreach (string suffix in new[] { @"Cache\Cache_Data", "Code Cache", "GPUCache" }) yield return Path.Combine(profile,suffix);
    }
    private static IEnumerable<string> Firefox()
    {
        string root = Path.Combine(Local,@"Mozilla\Firefox\Profiles");
        if (!Directory.Exists(root) || TempScanner.HasReparseAncestor(root)) return [];
        try { return Directory.GetDirectories(root).Select(p => Path.Combine(p,"cache2")).ToArray(); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { return []; }
    }
    public static readonly IReadOnlyList<CleanupCategory> All =
    [
        new("user-temp","User temporary files","Old files in this account's temporary directory. Recent/locked/system files remain protected.",true,()=>L("Temp")),
        new("windows-temp","Windows temporary files","Old Windows Temp files. Access can require administrator rights.",true,()=>[Path.Combine(Windows,"Temp")]),
        new("directx","DirectX shader cache","Rebuildable shader cache. First game launches can be slower after deletion; close games first.",false,()=>L("D3DSCache")),
        new("nvidia","NVIDIA shader caches","Old DX/GL shader cache files; recreated by the graphics driver. Close games first.",false,()=>L(@"NVIDIA\DXCache",@"NVIDIA\GLCache",@"NVIDIA Corporation\NV_Cache")),
        new("amd","AMD shader caches","Old DX/GL shader caches; shader compilation can repeat after cleanup.",false,()=>L(@"AMD\DxCache",@"AMD\DxcCache",@"AMD\GLCache")),
        new("user-crashes","User crash dumps","Old diagnostic crash dumps. Removing them reduces information available for troubleshooting.",false,()=>L("CrashDumps")),
        new("system-crashes","Windows minidumps","Old system crash minidumps; diagnostic data will be lost.",false,()=>[Path.Combine(Windows,"Minidump")]),
        new("user-wer","User Windows Error Reporting","Old queued/archived error reports and report temporary files.",false,()=>L(@"Microsoft\Windows\WER\ReportArchive",@"Microsoft\Windows\WER\ReportQueue",@"Microsoft\Windows\WER\Temp")),
        new("system-wer","System Windows Error Reporting","Old machine error reports; diagnostic reports can be unavailable afterward.",false,()=>[Path.Combine(Common,@"Microsoft\Windows\WER\ReportArchive"),Path.Combine(Common,@"Microsoft\Windows\WER\ReportQueue")]),
        new("thumbnails","Explorer thumbnail caches","Only thumbcache_*.db files. Windows recreates thumbnails; open/locked databases are skipped.",false,()=>L(@"Microsoft\Windows\Explorer"),["thumbcache_*.db"]),
        new("icons","Explorer icon caches","Only iconcache_*.db files. Windows rebuilds icons; open/locked databases are skipped.",false,()=>L(@"Microsoft\Windows\Explorer"),["iconcache_*.db"]),
        new("chrome","Chrome caches (browser closed)","Default/Profile* HTTP, code and GPU caches only. Cookies/passwords/history/session storage are excluded.",false,()=>Chromium(@"Google\Chrome\User Data"),Processes:["chrome"]),
        new("edge","Edge caches (browser closed)","Default/Profile* HTTP, code and GPU caches only. Cookies/passwords/history/session storage are excluded.",false,()=>Chromium(@"Microsoft\Edge\User Data"),Processes:["msedge"]),
        new("brave","Brave caches (browser closed)","Rebuildable profile HTTP/code/GPU caches; personal browser databases are excluded.",false,()=>Chromium(@"BraveSoftware\Brave-Browser\User Data"),Processes:["brave"]),
        new("vivaldi","Vivaldi caches (browser closed)","Rebuildable Default/Profile* caches; no bookmarks/passwords/session databases.",false,()=>Chromium(@"Vivaldi\User Data"),Processes:["vivaldi"]),
        new("firefox","Firefox cache2 (browser closed)","Local Firefox HTTP caches only; roaming profiles and personal browser data are excluded.",false,Firefox,Processes:["firefox"]),
        new("discord","Discord caches (Discord closed)","Only Discord Cache, Code Cache and GPUCache. Account/settings folders are preserved.",false,()=>R(@"discord\Cache",@"discord\Code Cache",@"discord\GPUCache"),Processes:["Discord","DiscordCanary","DiscordPTB"]),
        new("vscode","VS Code caches (editor closed)","Cache, CachedData, Code Cache and GPUCache only. User settings/extensions and projects are preserved.",false,()=>R(@"Code\Cache",@"Code\CachedData",@"Code\Code Cache",@"Code\GPUCache"),Processes:["Code"]),
        new("npm","npm download cache and logs","Old npm _cacache/_logs files. Future installs may download content again; project node_modules is excluded.",false,()=>L(@"npm-cache\_cacache",@"npm-cache\_logs"),Processes:["node"]),
        new("pip","pip download cache","Old pip cache files; future package installs can download/build content again.",false,()=>L(@"pip\Cache"),Processes:["pip","pip3"]),
        new("nuget-http","NuGet HTTP cache","Only the NuGet v3 HTTP cache; global packages and project files are preserved.",false,()=>L(@"NuGet\v3-cache"),Processes:["dotnet","devenv","MSBuild"]),
        new("update-downloads","Idle Windows Update download cache","Old downloads only when Windows Update/BITS are stopped and no restart is pending. Services are never stopped automatically.",false,()=>[Path.Combine(Windows,@"SoftwareDistribution\Download")],Services:["wuauserv","BITS"])
    ];
    public static IReadOnlyList<CleanupLocation> Locations() => All.SelectMany(category => category.Roots().Select(root =>
        new CleanupLocation(category,Path.GetFullPath(root)))).ToArray();
    public static bool Matches(CleanupLocation location, string path) => TempScanner.IsInside(path,location.Root)
        && (location.Category.Patterns == null || location.Category.Patterns.Any(pattern => FileSystemName.MatchesSimpleExpression(pattern,Path.GetFileName(path),true)));
    public static string? BlockReason(CleanupCategory category)
    {
        foreach (string name in category.Processes ?? [])
        {
            try
            {
                Process[] processes = Process.GetProcessesByName(name);
                bool running = processes.Length > 0; foreach (Process process in processes) process.Dispose();
                if (running) return "Close " + name + " before scanning/deleting this cache.";
            }
            catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException)
            { return "Running process state could not be verified."; }
        }
        return CleanupServiceGate.BlockReason(category.Services ?? [],ReadServiceState,DiagnosticsService.IsRestartPending);
    }
    private static uint ReadServiceState(string name)
    {
        // QUERY_STATUS only: this path never reads/writes a service configuration or starts/stops it.
        using var manager = NativeMethods.OpenSCManager(null,null,1);
        if (manager.IsInvalid) throw new Win32Exception(Marshal.GetLastWin32Error());
        using var service = NativeMethods.OpenService(manager,name,4);
        if (service.IsInvalid || !NativeMethods.QueryServiceStatus(service,out var status))
            throw new Win32Exception(Marshal.GetLastWin32Error());
        return status.State;
    }
}
