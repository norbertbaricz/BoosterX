using BoosterX.Core;
using Microsoft.Win32;

namespace BoosterX.App.Services;

public sealed record StartupItem(string Name, string Command, string Location, RegistryTarget Target, bool Protected);

public static class StartupService
{
    // A conservative blocklist protects known security/antivirus startup entries from this UI.
    private static readonly string[] ProtectedMarkers =
    ["securityhealth", "windows defender", "msascuil", "msmpeng", "antivirus", "malwarebytes", "bitdefender",
        "kaspersky", "avast", "avgui", "eset", "norton", "mcafee", "sophos", "crowdstrike", "sentinelone", "safebreach"];

    public static bool IsProtected(string name, string command) => ProtectedMarkers.Any(s =>
        name.Contains(s, StringComparison.OrdinalIgnoreCase) || command.Contains(s, StringComparison.OrdinalIgnoreCase));

    public static IReadOnlyList<StartupItem> List()
    {
        var result = new List<StartupItem>();
        foreach (var scope in new[] { ("HKCU", RegistryView.Registry64), ("HKLM", RegistryView.Registry64), ("HKLM", RegistryView.Registry32) })
        {
            var rootTarget = new RegistryTarget(scope.Item1, RegistryService.RunPath, "", scope.Item2);
            using var hive = RegistryService.OpenHive(rootTarget);
            using var key = hive.OpenSubKey(RegistryService.RunPath);
            if (key == null) continue;
            foreach (string name in key.GetValueNames())
            {
                string command = key.GetValue(name, null, RegistryValueOptions.DoNotExpandEnvironmentNames) as string ?? "";
                result.Add(new(name, command, $"{scope.Item1} / {(scope.Item2 == RegistryView.Registry64 ? "64-bit" : "32-bit")}",
                    rootTarget with { Name = name }, IsProtected(name, command)));
            }
        }
        return result.OrderBy(i => i.Name, StringComparer.OrdinalIgnoreCase).ToArray();
    }

    public static RegistrySnapshot Capture(StartupItem item)
    {
        if (item.Protected || IsProtected(item.Name, item.Command)) throw new InvalidOperationException("Security entries are protected.");
        RegistrySnapshot snapshot = RegistryService.Capture(item.Target);
        if (!snapshot.Exists || snapshot.Kind is not (RegistryValueKind.String or RegistryValueKind.ExpandString)
            || snapshot.Value.GetString() != item.Command)
            throw new InvalidOperationException("The startup entry changed. Refresh the list before trying again.");
        return snapshot;
    }
}
