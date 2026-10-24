using System.Text.Json;
using BoosterX.Core;

namespace BoosterX.App.Services;

public sealed class WindowsOperations(BoosterOptions options, Action<string> log, Action<double>? progress = null) : IUndoService
{
    public SystemCommand Commands { get; } = new(options, log, progress);
    public CleanupService Cleanup { get; } = new(options);
    public ServiceManager Services { get; } = new(options);
    public NetworkService Network => new(options, Commands);

    public IChangeStep Optimization(OptimizationDefinition definition)
    {
        if (Environment.OSVersion.Version.Build < definition.MinimumBuild) throw new InvalidOperationException("This preference requires a newer Windows build.");
        if (definition.RegistryWrites is { } create)
        {
            RegistryWrite[] writes = create(options).ToArray();
            return new WindowsStep(definition.Id, definition.Title, "registry", true,
                () => JsonDefaults.Element(writes.Select(w => RegistryService.Capture(w.Target)).ToArray()),
                _ =>
                {
                    foreach (RegistryWrite write in writes)
                    {
                        RegistryService.Write(write);
                        RegistrySnapshot actual = RegistryService.Capture(write.Target);
                        if (!actual.Exists || actual.Kind != write.Kind || actual.Value.GetRawText() != JsonDefaults.Element(write.Value).GetRawText())
                            throw new IOException("Windows did not retain " + write.Target.Name + ".");
                    }
                    return Task.FromResult(new StepOutcome("Preference values saved and verified. Sign out to fully apply; Windows build/policy support can vary."));
                });
        }
        return definition.Id switch
        {
            "power" => new WindowsStep("power", definition.Title, "power", true,
                () => JsonDefaults.Element(PowerService.Capture()), _ => Task.FromResult(PowerService.EnableHighPerformance())),
            "diagnostics" => new WindowsStep("diagnostics", definition.Title, "service", true,
                () => JsonDefaults.Element(ServiceManager.Capture("DiagTrack")), async state =>
                {
                    ServiceSnapshot previous = JsonDefaults.Read<ServiceSnapshot>(state);
                    if (previous.StartMode == 4) return new("Already disabled.", Skipped: true);
                    await Services.SetAsync(previous with { StartMode = 4, WasRunning = false });
                    return new("DiagTrack disabled; previous configuration and state are backed up.");
                }),
            _ => throw new InvalidOperationException("Unknown optimization.")
        };
    }

    public IChangeStep DisableStartup(StartupItem item) => new WindowsStep(
        "startup-" + Guid.NewGuid().ToString("N"), "Disable startup: " + item.Name, "registry", true,
        () => JsonDefaults.Element(new[] { StartupService.Capture(item) }), _ =>
        {
            RegistryService.Delete(item.Target);
            if (RegistryService.Capture(item.Target).Exists) throw new IOException("Startup entry could not be removed.");
            return Task.FromResult(new StepOutcome("Run entry disabled. The application may recreate it; use History to restore it."));
        });

    public IChangeStep SetDns(AdapterItem adapter, DnsPreset preset) => new WindowsStep("dns", "IPv4 DNS: " + adapter.Name,
        "dns", true, () => JsonDefaults.Element(NetworkService.Capture(adapter.Id)), async state =>
        {
            // Preserve dynamic-DNS registration as well as backing up the DNS server source/order.
            await Network.SetDnsAsync(adapter.Id, preset.Servers, JsonDefaults.Read<DnsSnapshot>(state).RegistrationMode);
            return new("IPv4 DNS saved and verified. IPv6 DNS is unchanged; no bandwidth/ping improvement is guaranteed.");
        });

    public IChangeStep CleanTemp(IReadOnlyList<TempCandidate> files) => new WindowsStep("temp", "Delete selected old temporary files", "cleanup", false,
        () => JsonDefaults.Element(new { Files = files.Count, Bytes = files.Sum(f => f.Length), MinimumAgeDays = options.TempMinimumAgeDays }), _ =>
        {
            CleanupResult result = Cleanup.Delete(files, log,(done,total) => progress?.Invoke(total > 0 ? 100d * done / total : 100));
            return Task.FromResult(new StepOutcome($"Deleted {result.Deleted:N0} files / {CleanupService.FormatBytes(result.Bytes)} logical bytes; {result.Skipped:N0} skipped. Deletions cannot be undone."));
        });

    public static IChangeStep EmptyRecycleBin() => new WindowsStep("recycle", "Empty Recycle Bin for this account", "cleanup", false,
        () => { var size = CleanupService.GetRecycleBin(); return JsonDefaults.Element(new { size.Items, size.Bytes }); }, _ =>
        {
            CleanupService.EmptyRecycleBin();
            return Task.FromResult(new StepOutcome("Recycle Bin emptied. Deleted files cannot be restored through BoosterX."));
        });

    public static IChangeStep RemoveApp(OptionalApp app,Action<double>? progress = null) => new WindowsStep("app-" + app.FullName,
        "Uninstall " + app.Name + " (" + app.Scope + ")", "appx", false, () =>
        {
            if (app.Protected) throw new InvalidOperationException(app.Protection);
            return JsonDefaults.Element(app);
        }, async _ =>
        {
            return await AppPackageService.RemoveAsync(app,progress);
        });

    public IChangeStep RepairLegacyService(string name) => new WindowsStep("repair-" + name,
        "Recover disabled service: " + name, "service", true,
        () => JsonDefaults.Element(ServiceManager.Capture(name)), async state =>
        {
            ServiceSnapshot previous = JsonDefaults.Read<ServiceSnapshot>(state);
            if (previous.StartMode != 4) return new("Skipped: service is not disabled; existing configuration preserved.", Skipped: true);
            var defaults = ServiceManager.LegacyRepairDefaults[name];
            await Services.SetAsync(previous with { StartMode = defaults.StartMode, Delayed = defaults.Delayed, WasRunning = defaults.StartMode == 2 });
            return new("Service enabled with the documented BoosterX recovery preset. Previous state is backed up.");
        });

    public IChangeStep WindowsRepair(string id)
    {
        RepairDefinition? known = RepairCatalog.All.FirstOrDefault(action => action.Id == id);
        if (known != null)
        {
            string drive = Path.GetPathRoot(Environment.GetFolderPath(Environment.SpecialFolder.Windows))!.TrimEnd('\\');
            return CommandStep(id,known.Title,known.Executable,RepairCatalog.ArgumentsFor(known,drive),known.Servicing,known.Restart,known.ReadOnly);
        }
        return id switch
        {
        "dism-scan" => CommandStep(id, "Scan Windows component health", "dism.exe", ["/Online", "/Cleanup-Image", "/ScanHealth", "/English", "/NoRestart"], true),
        "dism-repair" => CommandStep(id, "Repair Windows component store", "dism.exe", ["/Online", "/Cleanup-Image", "/RestoreHealth", "/English", "/NoRestart"], true),
        "sfc" => CommandStep(id, "Check and repair system files", "sfc.exe", ["/scannow"], true),
        "components" => CommandStep(id, "Clean superseded Windows components", "dism.exe", ["/Online", "/Cleanup-Image", "/StartComponentCleanup", "/English", "/NoRestart"], true),
        "winsock" => CommandStep(id, "Reset Winsock catalog", "netsh.exe", ["winsock", "reset"], false, true),
        "disk-scan" => CommandStep(id, "Scan system disk online", "chkdsk.exe", [Path.GetPathRoot(Environment.GetFolderPath(Environment.SpecialFolder.Windows))!.TrimEnd('\\'), "/scan"], true),
        "flush-dns" => new WindowsStep(id, "Flush DNS resolver cache", "network-repair", false,
            () => JsonDefaults.Element(new { }), _ =>
            {
                if (!NativeMethods.DnsFlushResolverCache()) throw new IOException("Windows could not flush the resolver cache.");
                return Task.FromResult(new StepOutcome("DNS resolver cache cleared."));
            }),
        _ => throw new InvalidOperationException("Unknown repair action.")
        };
    }

    public async Task UndoAsync(ChangeEntry entry, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        switch (entry.Kind)
        {
            case "registry":
                var failures = new List<Exception>();
                foreach (RegistrySnapshot value in JsonDefaults.Read<RegistrySnapshot[]>(entry.State).Reverse())
                    try { RegistryService.Restore(value); }
                    catch (Exception ex) { failures.Add(ex); }
                if (failures.Count > 0) throw new AggregateException("One or more registry values could not be restored.", failures);
                break;
            case "power": PowerService.Set(JsonDefaults.Read<PowerSnapshot>(entry.State).Active); break;
            case "service": await Services.SetAsync(JsonDefaults.Read<ServiceSnapshot>(entry.State)); break;
            case "dns":
                DnsSnapshot dns = JsonDefaults.Read<DnsSnapshot>(entry.State);
                await Network.SetDnsAsync(dns.AdapterId, dns.StaticServers, dns.RegistrationMode);
                break;
            default: throw new InvalidDataException("This operation cannot be restored by BoosterX.");
        }
    }

    private IChangeStep CommandStep(string id, string title, string executable, string[] arguments, bool servicing, bool restart = false, bool readOnly = false)
        => new WindowsStep(id, title, readOnly ? "windows-diagnostic" : "windows-repair", false,
            () => JsonDefaults.Element(new { executable, arguments, UndoAvailable = false }), async _ =>
            {
                int code = await Commands.RunAsync(executable, arguments, servicing);
                StepOutcome result = SystemCommand.CheckExit(code, "Windows command completed.", outputNeedsReview: true);
                return result with { RestartRequired = result.RestartRequired || restart };
            });
}

public sealed class WindowsStep(string id, string title, string kind, bool canUndo, Func<JsonElement> capture,
    Func<JsonElement, Task<StepOutcome>> apply) : IChangeStep
{
    public string Id => id;
    public string Title => title;
    public string Kind => kind;
    public bool CanUndo => canUndo;
    public Task<JsonElement> CaptureAsync(CancellationToken cancellationToken)
    { cancellationToken.ThrowIfCancellationRequested(); return Task.FromResult(capture()); }
    public Task<StepOutcome> ApplyAsync(JsonElement state, CancellationToken cancellationToken) => apply(state);
}
