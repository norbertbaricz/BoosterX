using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using BoosterX.Core;
using Microsoft.Win32;

namespace BoosterX.App.Services;

public sealed record ServiceSnapshot(string Name, uint StartMode, bool Delayed, bool WasRunning);

public sealed class ServiceManager(BoosterOptions options)
{
    public static readonly IReadOnlyDictionary<string, (uint StartMode, bool Delayed)> LegacyRepairDefaults =
        new Dictionary<string, (uint, bool)>(StringComparer.OrdinalIgnoreCase)
        {
            ["wuauserv"] = (3, false), ["UsoSvc"] = (2, true), ["SysMain"] = (2, false),
            ["WSearch"] = (2, true), ["DiagTrack"] = (2, true)
        };

    public static ServiceSnapshot Capture(string name)
    {
        Validate(name);
        using var manager = NativeMethods.OpenSCManager(null, null, 1);
        using var service = NativeMethods.OpenService(manager, name, 4);
        if (manager.IsInvalid || service.IsInvalid) throw new Win32Exception(Marshal.GetLastWin32Error());
        if (!NativeMethods.QueryServiceStatus(service, out var status)) throw new Win32Exception(Marshal.GetLastWin32Error());
        using var hive = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);
        using var key = hive.OpenSubKey(@"SYSTEM\CurrentControlSet\Services\" + name)
                        ?? throw new IOException("Service configuration was not found: " + name);
        uint start = Convert.ToUInt32(key.GetValue("Start") ?? throw new IOException("Missing service start mode."));
        if (start is < 2 or > 4 || status.State is not (1 or 4))
            throw new IOException("Service has an unsupported/transitioning state. Wait and retry: " + name);
        return new(name, start,
            Convert.ToInt32(key.GetValue("DelayedAutoStart", 0)) != 0, status.State == 4);
    }

    public async Task SetAsync(ServiceSnapshot state)
    {
        Validate(state.Name);
        if (state.StartMode is < 2 or > 4) throw new InvalidDataException("Unsupported service start mode.");
        ServiceSnapshot existing = Capture(state.Name);
        if (existing == state) return;
        using var manager = NativeMethods.OpenSCManager(null, null, 1);
        using var service = NativeMethods.OpenService(manager, state.Name, 2 | 4 | 16 | 32);
        if (manager.IsInvalid || service.IsInvalid) throw new Win32Exception(Marshal.GetLastWin32Error());
        if (!NativeMethods.ChangeServiceConfig(service, uint.MaxValue, state.StartMode, uint.MaxValue, null, null,
                IntPtr.Zero, null, null, null, null)) throw new Win32Exception(Marshal.GetLastWin32Error());
        var delayed = new NativeMethods.DelayedStart { Enabled = state.Delayed ? 1 : 0 };
        // The flag is stored for any service and only takes effect in Automatic mode.
        if (!NativeMethods.ChangeServiceConfig2(service, 3, ref delayed)) throw new Win32Exception(Marshal.GetLastWin32Error());
        if (!NativeMethods.QueryServiceStatus(service, out var current)) throw new Win32Exception(Marshal.GetLastWin32Error());
        uint desired = state.WasRunning && state.StartMode != 4 ? 4u : 1u;
        if (current.State == desired) return;
        if (desired == 4)
        {
            if (!NativeMethods.StartService(service, 0, IntPtr.Zero) && Marshal.GetLastWin32Error() != 1056)
                throw new Win32Exception(Marshal.GetLastWin32Error());
        }
        else if (!NativeMethods.ControlService(service, 1, out _) && Marshal.GetLastWin32Error() != 1062)
            throw new Win32Exception(Marshal.GetLastWin32Error());

        var clock = Stopwatch.StartNew();
        while (clock.Elapsed.TotalSeconds < options.ServiceTimeoutSeconds)
        {
            if (!NativeMethods.QueryServiceStatus(service, out current)) throw new Win32Exception(Marshal.GetLastWin32Error());
            if (current.State == desired) return;
            await Task.Delay(150).ConfigureAwait(false);
        }
        throw new TimeoutException("Timed out waiting for " + state.Name + ". Windows may still be processing the request.");
    }

    private static void Validate(string name)
    {
        if (!LegacyRepairDefaults.ContainsKey(name)) throw new InvalidDataException("Service is outside BoosterX's allowlist.");
    }
}
