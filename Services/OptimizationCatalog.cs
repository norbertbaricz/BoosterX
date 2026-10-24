using BoosterX.Core;
using Microsoft.Win32;

namespace BoosterX.App.Services;

public sealed record OptimizationDefinition(string Id, string Title, string Description, bool Recommended,
    Func<BoosterOptions, IReadOnlyList<RegistryWrite>>? RegistryWrites = null, string Category = "General", int MinimumBuild = 19041);

public static class OptimizationCatalog
{
    // The pure core catalog is also verified on non-Windows build hosts.
    public static readonly IReadOnlyList<OptimizationDefinition> All = PreferenceCatalog.All.Select(definition =>
        new OptimizationDefinition(definition.Id, definition.Title, definition.Description, definition.Recommended,
            definition.Values.Count == 0 ? null : options => definition.Values.Select(value => new RegistryWrite(
                new RegistryTarget("HKCU", value.Path, value.Name), Enum.Parse<RegistryValueKind>(value.Kind),
                PreferenceCatalog.ResolveValue(value, options))).ToArray(), definition.Category, definition.MinimumBuild)).ToArray();

    public static readonly IReadOnlyList<RegistryTarget> RegistryTargets = All
        .Where(d => d.RegistryWrites != null).SelectMany(d => d.RegistryWrites!(new BoosterOptions()))
        .Select(w => w.Target).ToArray();

    // Only optional consumer apps are offered. Store, Defender, Edge, WebView, runtimes and Xbox services are excluded.
    public static readonly IReadOnlyDictionary<string, string> OptionalApps = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["Microsoft.BingNews"] = "Microsoft News", ["Microsoft.BingWeather"] = "Weather",
        ["Microsoft.GetHelp"] = "Get Help", ["Microsoft.Getstarted"] = "Tips / Get Started",
        ["Microsoft.MicrosoftOfficeHub"] = "Microsoft 365 hub", ["Microsoft.MicrosoftSolitaireCollection"] = "Solitaire Collection",
        ["Microsoft.People"] = "People", ["Microsoft.SkypeApp"] = "Skype", ["Microsoft.Todos"] = "Microsoft To Do",
        ["Microsoft.WindowsFeedbackHub"] = "Feedback Hub", ["Microsoft.WindowsMaps"] = "Maps",
        ["Microsoft.YourPhone"] = "Phone Link", ["Microsoft.ZuneMusic"] = "Media Player",
        ["Microsoft.ZuneVideo"] = "Movies & TV", ["Clipchamp.Clipchamp"] = "Clipchamp",
        ["MicrosoftTeams"] = "Teams (consumer)", ["MSTeams"] = "Microsoft Teams",
        ["Microsoft.OutlookForWindows"] = "Outlook for Windows", ["Microsoft.Copilot"] = "Microsoft Copilot"
    };
}
