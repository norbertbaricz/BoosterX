using System.Text.Json;
using BoosterX.Core;
using Microsoft.Win32;

namespace BoosterX.App.Services;

public sealed record RegistryTarget(string Hive, string Path, string Name, RegistryView View = RegistryView.Registry64);
public sealed record RegistryWrite(RegistryTarget Target, RegistryValueKind Kind, object Value);
public sealed record RegistrySnapshot(RegistryTarget Target, bool Exists, RegistryValueKind Kind, JsonElement Value);

public static class RegistryService
{
    public const string RunPath = @"Software\Microsoft\Windows\CurrentVersion\Run";

    public static RegistryKey OpenHive(RegistryTarget target) => RegistryKey.OpenBaseKey(target.Hive switch
    {
        "HKCU" => RegistryHive.CurrentUser,
        "HKLM" => RegistryHive.LocalMachine,
        _ => throw new InvalidDataException("Unsupported registry hive.")
    }, target.View);

    public static RegistrySnapshot Capture(RegistryTarget target)
    {
        ValidateTarget(target);
        using RegistryKey hive = OpenHive(target);
        using RegistryKey? key = hive.OpenSubKey(target.Path);
        bool exists = key?.GetValueNames().Contains(target.Name, StringComparer.OrdinalIgnoreCase) == true;
        return exists
            ? new(target, true, key!.GetValueKind(target.Name), JsonDefaults.Element(key.GetValue(target.Name, null, RegistryValueOptions.DoNotExpandEnvironmentNames)))
            : new(target, false, RegistryValueKind.None, JsonDefaults.Element<object?>(null));
    }

    public static void Write(RegistryWrite write)
    {
        ValidateTarget(write.Target);
        using RegistryKey hive = OpenHive(write.Target);
        using RegistryKey key = hive.CreateSubKey(write.Target.Path, writable: true);
        key.SetValue(write.Target.Name, write.Value, write.Kind);
        key.Flush();
    }

    public static void Delete(RegistryTarget target)
    {
        ValidateTarget(target);
        using RegistryKey hive = OpenHive(target);
        using RegistryKey? key = hive.OpenSubKey(target.Path, writable: true);
        key?.DeleteValue(target.Name, throwOnMissingValue: false);
        key?.Flush();
    }

    public static void Restore(RegistrySnapshot snapshot)
    {
        ValidateTarget(snapshot.Target);
        if (!snapshot.Exists)
        {
            Delete(snapshot.Target);
            if (Capture(snapshot.Target).Exists) throw new IOException("Windows did not remove the originally absent registry value.");
            return;
        }
        object value = snapshot.Kind switch
        {
            RegistryValueKind.DWord => snapshot.Value.GetInt32(),
            RegistryValueKind.QWord => snapshot.Value.GetInt64(),
            RegistryValueKind.Binary or RegistryValueKind.None => snapshot.Value.GetBytesFromBase64(),
            RegistryValueKind.MultiString => snapshot.Value.Deserialize<string[]>() ?? [],
            RegistryValueKind.String or RegistryValueKind.ExpandString => snapshot.Value.GetString() ?? "",
            _ => throw new InvalidDataException("Unsupported registry value type.")
        };
        Write(new(snapshot.Target, snapshot.Kind, value));
        RegistrySnapshot actual = Capture(snapshot.Target);
        if (!actual.Exists || actual.Kind != snapshot.Kind || !JsonElement.DeepEquals(actual.Value,snapshot.Value))
            throw new IOException("Windows did not retain the original registry type/data for " + snapshot.Target.Name + ".");
    }

    public static void ValidateTarget(RegistryTarget target)
    {
        bool run = target.Path.Equals(RunPath, StringComparison.OrdinalIgnoreCase)
                   && target.Hive is "HKCU" or "HKLM";
        bool known = OptimizationCatalog.RegistryTargets.Any(t => t.Hive == target.Hive && t.View == target.View
            && t.Path.Equals(target.Path, StringComparison.OrdinalIgnoreCase)
            && t.Name.Equals(target.Name, StringComparison.OrdinalIgnoreCase));
        if ((!run && !known) || target.Name.Length > 16_383
            || target.View is not (RegistryView.Registry64 or RegistryView.Registry32))
            throw new InvalidDataException("Registry target is outside BoosterX's supported settings.");
        if (run && StartupService.IsProtected(target.Name, ""))
            throw new InvalidDataException("Security startup entries are protected.");
    }
}
