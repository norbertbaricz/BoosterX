using System.Diagnostics;
using System.ComponentModel;
using BoosterX.Core;
using Microsoft.Win32;

namespace BoosterX.App.Services;

public static class DesktopAppService
{
    private const string UninstallRoot = @"Software\Microsoft\Windows\CurrentVersion\Uninstall";
    public static IReadOnlyList<OptionalApp> List(Action<string>? log = null)
    {
        var result = new List<OptionalApp>();
        foreach (RegistryHive hive in new[] { RegistryHive.CurrentUser,RegistryHive.LocalMachine })
        foreach (RegistryView view in new[] { RegistryView.Registry64,RegistryView.Registry32 })
        {
            try
            {
                using var baseKey = RegistryKey.OpenBaseKey(hive,view); using RegistryKey? root = baseKey.OpenSubKey(UninstallRoot);
                if (root == null) continue;
                foreach (string id in root.GetSubKeyNames())
                {
                    try { OptionalApp? app = Read(hive,view,id); if (app != null) result.Add(app); }
                    catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException)
                    { log?.Invoke("Uninstall inventory entry skipped: " + id + ": " + ex.Message); }
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException)
            { log?.Invoke("Uninstall inventory: " + ex.Message); }
        }
        // HKCU views can alias; MSI product identities and identical registered commands are deduplicated per scope.
        return result.DistinctBy(app => app.Scope + "|" + app.Name + "|" + app.Version + "|" + app.UninstallString,StringComparer.OrdinalIgnoreCase).ToArray();
    }
    private static OptionalApp? Read(RegistryHive hive, RegistryView view, string id)
    {
        using var baseKey = RegistryKey.OpenBaseKey(hive,view); using RegistryKey? key = baseKey.OpenSubKey(UninstallRoot+"\\"+id);
        if (key == null) return null;
        string Text(string name) => key.GetValue(name) as string ?? "";
        string name = Text("DisplayName"); if (string.IsNullOrWhiteSpace(name)) return null;
        bool system = key.GetValue("SystemComponent") is int value && value != 0
            || Text("ParentKeyName").Length > 0 || Text("ReleaseType").Contains("Update",StringComparison.OrdinalIgnoreCase)
            || Text("ReleaseType").Contains("Hotfix",StringComparison.OrdinalIgnoreCase);
        bool msi = key.GetValue("WindowsInstaller") is int installer && installer == 1 && Guid.TryParse(id,out _);
        string uninstall = msi ? "MSI:"+Guid.Parse(id).ToString("B") : Environment.ExpandEnvironmentVariables(Text("UninstallString"));
        string? protection = AppInventoryPolicy.ProtectionReason(name,system);
        if (protection == null)
        {
            if (uninstall.Length == 0) protection = "No registered uninstaller; manage through Windows Installed apps";
            else if (!msi)
                try { _ = AppInventoryPolicy.Parse(uninstall); }
                catch (InvalidDataException ex) { protection = ex.Message; }
        }
        string hiveName = hive == RegistryHive.CurrentUser ? "HKCU" : "HKLM";
        int bits = view == RegistryView.Registry64 ? 64 : 32;
        return new(name,id,$"{hiveName}:{bits}:{id}","",Text("DisplayVersion"),msi ? InstalledAppKind.Msi : InstalledAppKind.Desktop,
            hive == RegistryHive.CurrentUser ? "Current user" : "All users",Text("Publisher"),protection != null,
            protection ?? "Runs the publisher's uninstaller; shared files and personal profiles may remain",hiveName,bits,id,uninstall,Text("InstallLocation"));
    }
    public static async Task<StepOutcome> RemoveAsync(OptionalApp selected)
    {
        if (selected.Hive is not ("HKCU" or "HKLM") || selected.View is not (32 or 64)
            || selected.RegistryKey.Contains('\\') || selected.RegistryKey.Contains('/')) throw new InvalidDataException("Invalid installed-app identity.");
        RegistryHive hive = selected.Hive == "HKCU" ? RegistryHive.CurrentUser : RegistryHive.LocalMachine;
        RegistryView view = selected.View == 64 ? RegistryView.Registry64 : RegistryView.Registry32;
        OptionalApp current = Read(hive,view,selected.RegistryKey) ?? throw new IOException("The application was removed or changed. Refresh the list.");
        if (current.Protected || current.UninstallString != selected.UninstallString || current.Name != selected.Name)
            throw new InvalidOperationException("The registered uninstall information changed or is protected. Refresh and review the application again.");
        ProcessStartInfo start;
        if (current.Kind == InstalledAppKind.Msi)
        {
            Guid product = Guid.Parse(current.RegistryKey);
            start = new(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System),"msiexec.exe"))
            { Arguments = "/x " + product.ToString("B") + " /norestart",UseShellExecute = true };
        }
        else
        {
            UninstallCommand command = AppInventoryPolicy.Parse(current.UninstallString);
            if (!File.Exists(command.Executable)) throw new FileNotFoundException("The registered uninstaller was not found.",command.Executable);
            start = new(command.Executable) { Arguments = command.Arguments,UseShellExecute = true,WorkingDirectory = Path.GetDirectoryName(command.Executable)! };
        }
        using Process process = Process.Start(start) ?? throw new IOException("Windows did not start the publisher's uninstaller.");
        await process.WaitForExitAsync().ConfigureAwait(false); // Do not kill an installer halfway through a removal.
        if (process.ExitCode == 1602) return new("The publisher's uninstaller was cancelled.",Skipped:true);
        if (process.ExitCode is not (0 or 3010 or 1641)) throw new Win32Exception(process.ExitCode,"The publisher's uninstaller reported an error.");
        if (Read(hive,view,current.RegistryKey) is { Protected:false })
            throw new IOException("The uninstaller returned, but this application is still registered. Finish any publisher prompts and refresh; no leftover folders were force-deleted.");
        return new("Publisher uninstaller completed for " + current.Scope + ". Shared dependencies/profiles were not force-deleted.",process.ExitCode is 3010 or 1641);
    }
}
