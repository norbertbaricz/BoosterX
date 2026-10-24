using BoosterX.App.Controls;

namespace BoosterX.App;

public partial class Form1
{
    private void InitializeActionIcons()
    {
        (RoundedButton Button, UiIcon Icon)[] actions =
        [
            (overviewNavButton, UiIcon.Dashboard), (optimizeNavButton, UiIcon.Sparkles),
            (cleanupNavButton, UiIcon.Trash), (appsNavButton, UiIcon.Apps), (startupNavButton, UiIcon.Startup),
            (networkNavButton, UiIcon.Network), (repairsNavButton, UiIcon.Wrench),
            (historyNavButton, UiIcon.History), (activityNavButton, UiIcon.Activity),
            (elevateButton, UiIcon.Shield), (logToggle, UiIcon.Activity), (responsiveMenuButton, UiIcon.Menu),
            (stopButton, UiIcon.Stop), (saveLogButton, UiIcon.Save),
            (analyzeButton, UiIcon.Search), (reviewPlanButton, UiIcon.Checklist),
            (saveReportButton, UiIcon.Save), (settingsButton, UiIcon.Settings),
            (applyButton, UiIcon.Check), (recommendedButton, UiIcon.Checklist), (clearOptimizationsButton, UiIcon.Clear),
            (scanTempButton, UiIcon.Search), (deleteTempButton, UiIcon.Trash), (recycleButton, UiIcon.Trash), (storageButton, UiIcon.Disk),
            (findAppsButton, UiIcon.Search), (removeAppsButton, UiIcon.Trash), (storeAppButton, UiIcon.Store), (installedAppsButton, UiIcon.Apps),
            (refreshStartupButton, UiIcon.Refresh), (disableStartupButton, UiIcon.Disable), (startupSettingsButton, UiIcon.Settings),
            (diagnoseNetworkButton, UiIcon.Globe), (applyDnsButton, UiIcon.Dns),
            (refreshAdaptersButton, UiIcon.Refresh), (flushDnsButton, UiIcon.Clear),
            (dismScanButton, UiIcon.Search), (repairWindowsButton, UiIcon.Wrench), (diskScanButton, UiIcon.Disk),
            (componentsButton, UiIcon.Layers), (winsockButton, UiIcon.Network), (legacyServicesButton, UiIcon.Restore),
            (restorePointButton, UiIcon.Heart), (systemProtectionButton, UiIcon.Shield), (networkSettingsButton, UiIcon.Settings),
            (restoreLatestButton, UiIcon.Restore), (historyDetailsButton, UiIcon.Checklist),
            (refreshHistoryButton, UiIcon.Refresh), (openBackupsButton, UiIcon.Folder),
            (copyActivityButton, UiIcon.Copy), (clearActivityButton, UiIcon.Clear), (testNotificationButton, UiIcon.Bell)
        ];
        foreach (var action in actions) action.Button.Symbol = action.Icon;
    }
}
