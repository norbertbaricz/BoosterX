using Windows.Management.Deployment;
using Windows.ApplicationModel;
using BoosterX.Core;

namespace BoosterX.App.Services;

public enum InstalledAppKind { Appx, Desktop, Msi }
public sealed record OptionalApp(string Name, string PackageName, string FullName, string FamilyName, string Version,
    InstalledAppKind Kind = InstalledAppKind.Appx, string Scope = "Current user", string Publisher = "",
    bool Protected = false, string Protection = "", string Hive = "", int View = 64, string RegistryKey = "",
    string UninstallString = "", string InstallLocation = "");

public static class AppPackageService
{
    private static readonly string[] ProtectedPackages = ["Microsoft.WindowsStore","Microsoft.StorePurchaseApp","Microsoft.DesktopAppInstaller",
        "Microsoft.SecHealthUI","Microsoft.Windows.SecHealthUI","Microsoft.Windows.CloudExperienceHost","Microsoft.AAD.BrokerPlugin",
        "Microsoft.AccountsControl","Microsoft.Win32WebViewHost","Microsoft.Windows.ShellExperienceHost","Microsoft.Windows.StartMenuExperienceHost"];
    private static bool IsProtected(Package package) => package.IsFramework || package.IsResourcePackage
        || package.SignatureKind == PackageSignatureKind.System || ProtectedPackages.Contains(package.Id.Name,StringComparer.OrdinalIgnoreCase)
        || AppInventoryPolicy.ProtectionReason(package.Id.Name,false) != null;
    public static IReadOnlyList<OptionalApp> List(Action<string>? log = null)
    {
        var result = new List<OptionalApp>();
        try
        {
            var manager = new PackageManager();
            foreach (Package package in manager.FindPackagesForUser(string.Empty))
            {
                try
                {
                    if (package.IsResourcePackage) continue;
                    var v = package.Id.Version; string title;
                    try { title = package.DisplayName; } catch { title = package.Id.Name; }
                    if (string.IsNullOrWhiteSpace(title)) title = package.Id.Name;
                    if (OptimizationCatalog.OptionalApps.TryGetValue(package.Id.Name,out string? known)) title = known;
                    bool guarded = IsProtected(package);
                    result.Add(new(title,package.Id.Name,package.Id.FullName,package.Id.FamilyName,$"{v.Major}.{v.Minor}.{v.Build}.{v.Revision}",
                        Publisher:package.Id.Publisher,Protected:guarded,Protection:guarded ? "Windows/system/framework package" : "Publisher package removal; personal app data may be lost"));
                }
                catch (Exception ex) when (ex is System.Runtime.InteropServices.COMException or UnauthorizedAccessException or IOException)
                { log?.Invoke("Package inventory: " + ex.Message); }
            }
        }
        catch (Exception ex) when (ex is System.Runtime.InteropServices.COMException or UnauthorizedAccessException or IOException)
        { log?.Invoke("AppX inventory unavailable: " + ex.Message); }
        result.AddRange(DesktopAppService.List(log));
        return result.DistinctBy(app => app.FullName,StringComparer.OrdinalIgnoreCase).OrderBy(app => app.Name,StringComparer.CurrentCultureIgnoreCase).ToArray();
    }
    public static async Task<StepOutcome> RemoveAsync(OptionalApp app,Action<double>? progress = null)
    {
        if (app.Protected) throw new InvalidOperationException(app.Protection);
        if (app.Kind != InstalledAppKind.Appx) return await DesktopAppService.RemoveAsync(app);
        var manager = new PackageManager();
        Package? found = manager.FindPackagesForUser(string.Empty).FirstOrDefault(p => p.Id.FullName == app.FullName
            && p.Id.Name == app.PackageName && p.Id.FamilyName == app.FamilyName);
        if (found == null) throw new InvalidOperationException("The package changed or was already removed. Refresh the app list.");
        if (IsProtected(found)) throw new InvalidOperationException("This Windows/system/framework package is protected.");
        var operation = manager.RemovePackageAsync(app.FullName,RemovalOptions.None);
        operation.Progress = (_,value) => progress?.Invoke(value.percentage);
        var result = await operation;
        if (result.ExtendedErrorCode is { HResult: < 0 } error) throw new IOException(result.ErrorText,error);
        return new("Removed the current account's package. Personal app data may be lost; reinstall from Store if needed.");
    }
}
