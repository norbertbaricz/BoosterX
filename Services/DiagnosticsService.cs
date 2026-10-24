using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text;
using Microsoft.Win32;

namespace BoosterX.App.Services;

public sealed record SystemOverview(string Windows, string Processor, string Memory, string Disk, string Power,
    string Uptime, bool RestartPending, string Account);

public static class DiagnosticsService
{
    public static string UserSid => WindowsIdentity.GetCurrent().User?.Value ?? throw new IOException("Cannot identify the Windows account.");
    public static string MachineId
    {
        get
        {
            using var hive = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);
            using var key = hive.OpenSubKey(@"SOFTWARE\Microsoft\Cryptography");
            string value = key?.GetValue("MachineGuid") as string ?? throw new IOException("Cannot identify this PC for backup protection.");
            return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
        }
    }

    public static SystemOverview Overview()
    {
        Version version = Environment.OSVersion.Version;
        using var hive = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);
        using var os = hive.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion");
        using var cpu = hive.OpenSubKey(@"HARDWARE\DESCRIPTION\System\CentralProcessor\0");
        string osName = $"Windows {(version.Build >= 22000 ? "11" : "10")} {os?.GetValue("DisplayVersion")} / build {version.Build}.{os?.GetValue("UBR")} / {RuntimeInformation.OSArchitecture}";
        var memory = new NativeMethods.MemoryStatus { Length = (uint)Marshal.SizeOf<NativeMethods.MemoryStatus>() };
        string memoryLabel = NativeMethods.GlobalMemoryStatusEx(ref memory)
            ? $"{memory.TotalPhysical / (1024d * 1024 * 1024):0.0} GB total / {memory.AvailablePhysical / (1024d * 1024 * 1024):0.0} GB available"
            : "Unavailable";
        var disk = new DriveInfo(Path.GetPathRoot(Environment.GetFolderPath(Environment.SpecialFolder.Windows))!);
        string power;
        try
        {
            Guid active = PowerService.Capture().Active;
            power = active == PowerService.HighPerformance ? "High performance" : "Current plan: " + active;
        }
        catch (Exception ex) { power = ex.Message; }
        bool pending = IsRestartPending();
        return new(osName, (cpu?.GetValue("ProcessorNameString") as string)?.Trim() ?? Environment.GetEnvironmentVariable("PROCESSOR_IDENTIFIER") ?? "Unknown CPU",
            memoryLabel, $"{disk.Name} {disk.AvailableFreeSpace / (1024d * 1024 * 1024):0.0} GB free / {disk.TotalSize / (1024d * 1024 * 1024):0.0} GB total",
            power, TimeSpan.FromMilliseconds(Environment.TickCount64).ToString(@"d\d\ hh\h\ mm\m"), pending, WindowsIdentity.GetCurrent().Name);
    }

    public static bool IsRestartPending()
    {
        using var hive = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine,RegistryView.Registry64);
        using var cbs = hive.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Component Based Servicing\RebootPending");
        using var wu = hive.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\WindowsUpdate\Auto Update\RebootRequired");
        using var sessions = hive.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\Session Manager");
        return cbs != null || wu != null || sessions?.GetValue("PendingFileRenameOperations") != null;
    }

    public static async Task<string> AnalyzeAsync(CancellationToken cancellationToken)
    {
        SystemOverview info = Overview();
        var samples = new Dictionary<int, (string Name, TimeSpan Cpu)>();
        foreach (Process process in Process.GetProcesses())
        {
            using (process)
            {
                try { samples[process.Id] = (process.ProcessName, process.TotalProcessorTime); }
                catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception or NotSupportedException) { }
            }
        }
        var clock = Stopwatch.StartNew();
        await Task.Delay(750, cancellationToken);
        var processes = new List<(string Name, int Id, double Cpu, long Ram)>();
        foreach (var pair in samples)
        {
            try
            {
                using Process current = Process.GetProcessById(pair.Key);
                if (current.ProcessName != pair.Value.Name) continue;
                double cpu = (current.TotalProcessorTime - pair.Value.Cpu).TotalMilliseconds / clock.Elapsed.TotalMilliseconds / Environment.ProcessorCount * 100;
                processes.Add((current.ProcessName, current.Id, Math.Clamp(cpu, 0, 100), current.WorkingSet64));
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or System.ComponentModel.Win32Exception or NotSupportedException) { }
        }
        var lines = new List<string>
        {
            "BoosterX local diagnostic report — " + DateTimeOffset.Now.ToString("u"),
            info.Windows, info.Processor, "Memory: " + info.Memory, "System disk: " + info.Disk,
            "Power: " + info.Power, "Uptime: " + info.Uptime, "Account being optimized: " + info.Account,
            "Restart pending (best-effort detection): " + info.RestartPending, "", "Top CPU consumers (short sample; process CPU as % of all logical CPUs):"
        };
        lines.AddRange(processes.OrderByDescending(p => p.Cpu).Take(10).Select(p => $"{p.Name} / PID {p.Id}: {p.Cpu:0.0}% CPU, {p.Ram / (1024d * 1024):0} MB working set"));
        lines.Add("\nTop working sets (shared memory can be counted in more than one process):");
        lines.AddRange(processes.OrderByDescending(p => p.Ram).Take(10).Select(p => $"{p.Name} / PID {p.Id}: {p.Ram / (1024d * 1024):0} MB"));
        lines.Add("\nServices changed by the old BoosterX (read-only check):");
        foreach (string service in ServiceManager.LegacyRepairDefaults.Keys)
        {
            try { ServiceSnapshot state = ServiceManager.Capture(service); lines.Add($"{service}: start={state.StartMode} (2 auto / 3 manual / 4 disabled), running={state.WasRunning}"); }
            catch (Exception ex) { lines.Add(service + ": " + ex.Message); }
        }
        lines.Add("\nThis report diagnoses the current state. RAM usage and FPS are not fabricated as an optimization score.");
        return string.Join(Environment.NewLine, lines);
    }
}
