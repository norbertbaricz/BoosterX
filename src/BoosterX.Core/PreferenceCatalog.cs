using System.Globalization;

namespace BoosterX.Core;

public sealed record PreferenceValue(string Path, string Name, string Kind, object Value);
public sealed record PreferenceDefinition(string Id, string Title, string Category, string Description,
    bool Recommended, IReadOnlyList<PreferenceValue> Values, int MinimumBuild = 19041);

/// <summary>Reviewed user preferences, not benchmark claims. New choices are opt-in; originals retain their IDs.</summary>
public static class PreferenceCatalog
{
    private const string Desktop = @"Control Panel\Desktop";
    private const string Explorer = @"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced";
    private const string Content = @"Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager";
    private const string Search = @"Software\Microsoft\Windows\CurrentVersion\SearchSettings";
    private static PreferenceValue D(string path, string name, int value = 0) => new(path, name, "DWord", value);
    private static PreferenceValue S(string path, string name, string value) => new(path, name, "String", value);
    public static readonly IReadOnlyList<PreferenceDefinition> All =
    [
        new("menus", "Faster menus", "Desktop", "Use the configured menu delay (100 ms by default). Sign out to fully apply.", true,
            [S(Desktop,"MenuShowDelay","$MenuDelay")]),
        new("capture", "Disable background game recording", "Gaming", "Disable automatic Game DVR capture for this user. Manual recording can also be affected.", true,
            [D(@"Software\Microsoft\Windows\CurrentVersion\GameDVR","AppCaptureEnabled"),D(@"System\GameConfigStore","GameDVR_Enabled")]),
        new("suggestions", "Reduce Windows suggestions", "Suggestions", "Turn off the original supported suggestion/consumer-content preferences. Windows build/policy support varies.", true,
            [D(Content,"SubscribedContent-338388Enabled"),D(Content,"SubscribedContent-338389Enabled"),D(Content,"SilentInstalledAppsEnabled"),D(Content,"SystemPaneSuggestionsEnabled")]),
        new("animations", "Reduce window animations", "Desktop", "Disable minimize/maximize animation. Sign out; shutdown timeouts are preserved.", false,
            [S(Desktop+@"\WindowMetrics","MinAnimate","0")]),
        new("transparency", "Disable transparency", "Desktop", "Reduce translucent Windows surfaces for this user. Sign out to fully apply.", false,
            [D(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize","EnableTransparency")]),
        new("power", "High performance power plan", "Power", "Use an existing supported High performance plan on AC. More heat/power; skipped on battery or unsupported hardware.", false, []),
        new("diagnostics", "Disable diagnostic tracking service", "Privacy", "Optional DiagTrack preference with state backup. Can affect diagnostics; no guaranteed FPS benefit.", false, []),
        new("game-mode", "Enable Windows Game Mode", "Gaming", "Ask Windows to use Game Mode. Actual behavior depends on the game and Windows build.", false,
            [D(@"Software\Microsoft\GameBar","AutoGameModeEnabled",1)]),
        new("taskbar-animation", "Reduce taskbar animations", "Desktop", "Disable supported taskbar animation effects. Explorer restart or sign-out may be needed.", false,[D(Explorer,"TaskbarAnimations")]),
        new("drag-outlines", "Use window outlines while dragging", "Desktop", "Show an outline instead of full window contents during drag. This changes desktop behavior.", false,[S(Desktop,"DragFullWindows","0")]),
        new("icon-shadows", "Disable desktop icon text shadows", "Desktop", "Remove supported desktop icon-label shadows. Visibility depends on the wallpaper; sign out to apply.", false,[D(Explorer,"ListviewShadow")]),
        new("selection-transparency", "Disable translucent selection rectangles", "Desktop", "Use the simpler Explorer/desktop selection rectangle where supported. Sign out to apply.", false,[D(Explorer,"ListviewAlphaSelect")]),
        new("advertising-id", "Disable advertising ID", "Privacy", "Disable this account's advertising identifier preference. This is a privacy choice, not a speed measurement.", false,[D(@"Software\Microsoft\Windows\CurrentVersion\AdvertisingInfo","Enabled")]),
        new("tailored-experiences", "Disable tailored diagnostic experiences", "Privacy", "Stop using optional diagnostic data for personalized suggestions where Windows supports the preference.", false,[D(@"Software\Microsoft\Windows\CurrentVersion\Privacy","TailoredExperiencesWithDiagnosticDataEnabled")]),
        new("frequent-apps", "Stop tracking frequent app launches", "Privacy", "Disable Start app-launch tracking. Frequent/most-used app suggestions may disappear.", false,[D(Explorer,"Start_TrackProgs")]),
        new("recent-documents", "Stop showing recent documents", "Privacy", "Hide recent documents in supported Start/Jump Lists. Does not delete personal documents.", false,[D(Explorer,"Start_TrackDocs")]),
        new("search-history", "Disable device search history", "Search", "Disable the local Windows Search history preference; saved browser history is untouched.", false,[D(Search,"IsDeviceSearchHistoryEnabled")]),
        new("search-personal-cloud", "Disable personal-account cloud search", "Search", "Exclude personal Microsoft-account cloud content from Windows Search where supported.", false,[D(Search,"IsMSACloudSearchEnabled")]),
        new("search-work-cloud", "Disable work-account cloud search", "Search", "Exclude work/school cloud content from Windows Search where supported. Managed policy can override it.", false,[D(Search,"IsAADCloudSearchEnabled")]),
        new("search-highlights", "Disable Search highlights", "Search", "Hide dynamic Search highlights where supported; local file indexing is preserved.", false,[D(Search,"IsDynamicSearchBoxEnabled")],19044),
        new("explorer-suggestions", "Disable File Explorer provider suggestions", "Suggestions", "Hide supported sync-provider promotional notifications in File Explorer; sync clients remain installed.", false,[D(Explorer,"ShowSyncProviderNotifications")]),
        new("welcome-experience", "Disable post-update welcome suggestions", "Suggestions", "Hide the supported Windows welcome experience after updates/sign-in. Updates stay enabled.", false,[D(Content,"SubscribedContent-310093Enabled")]),
        new("settings-suggestions", "Reduce Settings app suggestions", "Suggestions", "Disable supported suggested-content preferences in Windows Settings. Availability varies by build.", false,[D(Content,"SubscribedContent-338393Enabled"),D(Content,"SubscribedContent-353694Enabled"),D(Content,"SubscribedContent-353696Enabled")]),
        new("lockscreen-suggestions", "Disable rotating lock-screen suggestions", "Suggestions", "Disable supported rotating lock-screen/content-overlay preferences; the sign-in security policy is preserved.", false,[D(Content,"RotatingLockScreenEnabled"),D(Content,"RotatingLockScreenOverlayEnabled")]),
        new("content-delivery", "Disable optional content delivery suggestions", "Suggestions", "Turn off optional content-delivery suggestions for this account. Store and Windows Update remain available.", false,[D(Content,"ContentDeliveryAllowed"),D(Content,"OemPreInstalledAppsEnabled"),D(Content,"PreInstalledAppsEnabled"),D(Content,"PreInstalledAppsEverEnabled")]),
        new("finish-setup", "Disable finish-setting-up reminders", "Suggestions", "Hide supported account setup reminders. Sign-in/account features themselves remain available.", false,[D(@"Software\Microsoft\Windows\CurrentVersion\UserProfileEngagement","ScoobeSystemSettingEnabled")]),
        new("restart-apps", "Disable automatic restart of apps at sign-in", "Startup", "Stop Windows automatically restarting supported apps after sign-in; existing Run entries are preserved.", false,[D(@"Software\Microsoft\Windows NT\CurrentVersion\Winlogon","RestartApps")]),
        new("explorer-recent", "Hide recent files in Explorer Home", "Explorer", "Hide recent-file suggestions in supported Explorer Home/Quick access views. No file is removed.", false,[D(@"Software\Microsoft\Windows\CurrentVersion\Explorer","ShowRecent")]),
        new("explorer-frequent", "Hide frequent folders in Explorer Home", "Explorer", "Hide frequent-folder suggestions in supported Explorer Home/Quick access views.", false,[D(@"Software\Microsoft\Windows\CurrentVersion\Explorer","ShowFrequent")]),
        new("explorer-cloud", "Hide Explorer cloud recommendations", "Explorer", "Hide supported cloud-file recommendations in Explorer Home. OneDrive files are preserved.", false,[D(@"Software\Microsoft\Windows\CurrentVersion\Explorer","ShowCloudFilesInQuickAccess")],22000),
        new("explorer-thispc", "Open File Explorer to This PC", "Explorer", "Start Explorer at This PC instead of Home/Quick access; this is a navigation preference.", false,[D(Explorer,"LaunchTo",1)]),
        new("edge-startup", "Disable Microsoft Edge startup boost", "Browser", "Set Edge's documented per-user startup-boost policy to disabled. Edge may display a managed-policy indicator.", false,[D(@"Software\Policies\Microsoft\Edge","StartupBoostEnabled")]),
        new("edge-background", "Disable Edge background mode", "Browser", "Disable Edge's documented background-mode policy. Background web apps/extensions can stop after the browser closes.", false,[D(@"Software\Policies\Microsoft\Edge","BackgroundModeEnabled")]),
        new("mouse-acceleration", "Disable pointer acceleration", "Input", "Optional input preference: disable enhanced pointer acceleration after sign-out. Pointer speed is preserved.", false,
            [S(@"Control Panel\Mouse","MouseSpeed","0"),S(@"Control Panel\Mouse","MouseThreshold1","0"),S(@"Control Panel\Mouse","MouseThreshold2","0")])
    ];
    public static object ResolveValue(PreferenceValue value, BoosterOptions options) => value.Value is string text && text == "$MenuDelay"
        ? options.MenuDelayMilliseconds.ToString(CultureInfo.InvariantCulture) : value.Value;
}
