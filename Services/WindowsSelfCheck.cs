using System.Runtime.InteropServices;
using System.Text.Json;
using BoosterX.Core;

namespace BoosterX.App.Services;

/// <summary>Windows-only smoke checks. Changes only the check's own temporary fixtures, never Windows settings.</summary>
public static class WindowsSelfCheck
{
    public static async Task<int> RunAsync(BoosterOptions options, string dataDirectory)
    {
        var checks = new List<object>();
        int failures = 0;
        async Task Check(string name, Func<Task<string>> test)
        {
            try { checks.Add(new { Name = name, Passed = true, Detail = await test() }); }
            catch (Exception ex) { failures++; checks.Add(new { Name = name, Passed = false, Detail = ex.ToString() }); }
        }
        Task<string> Read(Func<string> action) => Task.FromResult(action());
        await Check("Process COM callback security", () => Read(() =>
        {
            if (ComSecurityScope.SecurityResult < 0) Marshal.ThrowExceptionForHR(ComSecurityScope.SecurityResult);
            return "Local COM callback permissions initialized. No restore point was requested.";
        }));
        await Check("Native struct sizes", () => Read(() =>
        {
            if (Marshal.SizeOf<NativeMethods.MemoryStatus>() != 64 || Marshal.SizeOf<NativeMethods.FileInformation>() != 52
                || Marshal.SizeOf<NativeMethods.RecycleInfo>() != 24 || Marshal.SizeOf<NativeMethods.RestorePointInfo>() != 528
                || Marshal.SizeOf<NativeMethods.RestoreStatus>() != 16 || Marshal.SizeOf<NativeMethods.DnsInterfaceSettings>() != 64) throw new InvalidDataException("Native layout mismatch.");
            return "Memory, file, Recycle Bin and restore-point layouts match Win64 ABI.";
        }));
        await Check("System information", () => Read(() => JsonSerializer.Serialize(DiagnosticsService.Overview())));
        await Check("Power API enumeration", () => Read(() =>
        {
            Guid active = PowerService.Capture().Active;
            if (!PowerService.Exists(active)) throw new IOException("The active scheme was not enumerated.");
            return active.ToString();
        }));
        await Check("Supported registry preferences (read only)", () => Read(() =>
        {
            foreach (var target in OptimizationCatalog.RegistryTargets) _ = RegistryService.Capture(target);
            return "All preference snapshots readable.";
        }));
        await Check("Startup enumeration", () => Read(() => StartupService.List().Count + " Run entries."));
        await Check("AppX enumeration", () => Read(() => AppPackageService.List().Count + " installed package/desktop entries."));
        await Check("Network enumeration and DNS snapshots", () => Read(() =>
        {
            var adapters = NetworkService.ListAdapters();
            foreach (var adapter in adapters) _ = NetworkService.Capture(adapter.Id);
            return adapters.Count + " active Ethernet/Wi-Fi adapters. No DNS changed.";
        }));
        await Check("Service reads", () => Read(() =>
        {
            var details = new List<string>();
            foreach (string name in ServiceManager.LegacyRepairDefaults.Keys)
            {
                try { _ = ServiceManager.Capture(name); details.Add(name + ": readable"); }
                catch (System.ComponentModel.Win32Exception ex) when (ex.NativeErrorCode == 1060)
                { details.Add(name + ": not installed on this edition"); }
            }
            return string.Join("; ", details) + ". No services changed.";
        }));
        await Check("Native handle cleanup on owned fixtures", () => Read(() => CheckHandleCleanup(options)));
        string directory = Path.Combine(dataDirectory, "SelfCheck");
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory, "latest.json"), JsonSerializer.Serialize(new
        {
            Product = BoosterOptions.ProductVersion, DateUtc = DateTimeOffset.UtcNow,
            Architecture = RuntimeInformation.ProcessArchitecture.ToString(), Failures = failures, Checks = checks
        }, JsonDefaults.Options));
        return failures == 0 ? 0 : 1;
    }

    private static string CheckHandleCleanup(BoosterOptions options)
    {
        string root = CleanupService.AllowedRoots[0];
        Directory.CreateDirectory(root);
        string fixture = Path.Combine(root, "BoosterX-self-check-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(fixture);
        try
        {
            DateTime old = DateTime.UtcNow.AddDays(-options.TempMinimumAgeDays - 2);
            string MakeFile(string name)
            {
                string path = Path.Combine(fixture, name); File.WriteAllText(path, "check fixture");
                File.SetCreationTimeUtc(path, old); File.SetLastWriteTimeUtc(path, old); return path;
            }
            TempCandidate Candidate(string path)
            { var f = new FileInfo(path); return new(root, path, f.Length, f.LastWriteTimeUtc, f.CreationTimeUtc); }
            var cleanup = new CleanupService(options);
            string eligible = MakeFile("eligible.tmp");
            if (cleanup.Delete([Candidate(eligible)], _ => { }).Deleted != 1 || File.Exists(eligible))
                throw new IOException("Exclusive handle deletion failed.");
            string locked = MakeFile("locked.tmp"); TempCandidate lockedCandidate = Candidate(locked);
            using (var held = new FileStream(locked, FileMode.Open, FileAccess.Read, FileShare.None))
                if (cleanup.Delete([lockedCandidate], _ => { }).Deleted != 0) throw new IOException("Locked file was not protected.");
            string changed = MakeFile("changed.tmp"); TempCandidate stale = Candidate(changed); File.AppendAllText(changed, "changed");
            if (cleanup.Delete([stale], _ => { }).Deleted != 0 || !File.Exists(changed)) throw new IOException("Stale candidate was not protected.");
            string fresh = MakeFile("fresh.tmp"); File.SetCreationTimeUtc(fresh, DateTime.UtcNow);
            if (cleanup.Delete([Candidate(fresh)], _ => { }).Deleted != 0) throw new IOException("Recent file was not protected.");
            return "Old fixture deleted; locked, changed and recent fixtures preserved.";
        }
        finally { Directory.Delete(fixture, recursive: true); }
    }
}
