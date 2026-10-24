using System.Text.Json;
using BoosterX.Core;

namespace BoosterX.App.Services;

internal sealed class UiPreferences
{
    // User-interface preferences are separate from Windows tuning settings.
    public bool DesktopNotifications { get; set; } = true;
    public bool NotifyInForeground { get; set; }
    public bool FollowActivity { get; set; } = true;

    internal static UiPreferences Load(string directory)
    {
        string path = Path.Combine(directory, "ui-settings.json");
        if (!File.Exists(path)) return new();
        if (new FileInfo(path).Length > 64_000) throw new InvalidDataException("UI settings file is too large.");
        return JsonSerializer.Deserialize<UiPreferences>(File.ReadAllText(path), JsonDefaults.Options) ?? new();
    }
    internal void Save(string directory)
    {
        string path = Path.Combine(directory, "ui-settings.json"), temporary = path + ".new";
        File.WriteAllText(temporary, JsonSerializer.Serialize(this, JsonDefaults.Options));
        File.Move(temporary, path, overwrite: true);
    }
}
