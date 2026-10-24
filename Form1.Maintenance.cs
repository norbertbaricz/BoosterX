using BoosterX.App.Controls;
using BoosterX.App.Services;
using BoosterX.Core;

namespace BoosterX.App;

public partial class Form1
{
    private readonly DetailListPanel repairActionsList = new() { Name = "repairActionsList" };

    private void InitializeCleanupControls()
    {
        cleanupTitleLabel.Text = "Review temporary files and caches";
        cleanupDescriptionLabel.Text = $"Scan all {CleanupCatalog.All.Count} cleanup areas automatically. Active or unavailable caches are skipped. Review the file preview before deleting. Personal files and browser passwords/cookies/history are excluded. Rebuilt caches can slow the next launch; old diagnostic reports are included.";
        scanTempButton.Text = "Scan cleanup areas";
        cleanupGrid.Columns.Insert(2,TextColumn("cleanupAreaColumn","Area","Category",160));
        cleanupViewport.LogicalWheelStep = UiPalette.MaintenanceWheelStep;
    }

    private void InitializeRepairControls()
    {
        repairsTitleLabel.Text = "Windows maintenance & recovery";
        repairsDescriptionLabel.Text = "Run an action directly from its button. Each description explains what it checks or changes; results appear in Activity and History. Windows servicing is allowed to finish, and no automatic reboot is performed.";
        repairsViewport.LogicalWheelStep = UiPalette.MaintenanceWheelStep;
        var existing = new Dictionary<string,RoundedButton>(StringComparer.Ordinal)
        {
            ["dism-scan"] = dismScanButton,["disk-scan"] = diskScanButton,
            ["components"] = componentsButton,["winsock"] = winsockButton
        };
        AddRepairAction(repairWindowsButton,repairWindowsButtonDescription.Text,"Windows");
        foreach (RepairDefinition definition in RepairCatalog.All)
        {
            if (!existing.TryGetValue(definition.Id,out RoundedButton? button))
            {
                button = ActionButton("repairAction_"+definition.Id,definition.Title,definition.ReadOnly ? UiIcon.Search : UiIcon.Wrench);
                button.Click += async (_,_) => await GuardAsync(() => RunRepairDefinitionAsync(definition));
            }
            else button.Text = definition.Title;
            string effect = definition.ReadOnly ? "Read only. " : definition.Restart ? "Changes Windows; restart required. " : "Changes Windows; no BoosterX undo. ";
            AddRepairAction(button,effect+definition.Description,definition.Category);
        }
        AddRepairAction(legacyServicesButton,legacyServicesButtonDescription.Text,"Recovery and settings");
        AddRepairAction(restorePointButton,restorePointButtonDescription.Text,"Recovery and settings");
        AddRepairAction(systemProtectionButton,systemProtectionButtonDescription.Text,"Recovery and settings");
        AddRepairAction(networkSettingsButton,networkSettingsButtonDescription.Text,"Recovery and settings");

        // Reparent the retained actions before disposing the old nested scroll/table container.
        repairsPage.Controls.Remove(repairsScrollPanel); repairsScrollPanel.Dispose();
        repairsPage.Controls.Remove(repairsActions); repairsActions.Dispose();
        repairsPage.Controls.Clear(); repairsPage.RowStyles.Clear(); repairsPage.RowCount = 4;
        for (int i = 0; i < 4; i++) repairsPage.RowStyles.Add(new(SizeType.AutoSize));
        repairsPage.Controls.Add(repairsTitleLabel,0,0); repairsPage.Controls.Add(repairsDescriptionLabel,0,1);
        repairSearch.TextChanged += (_,_) => RefreshRepairFilter();
        repairsPage.Controls.Add(Flow("repairActionFilters",repairSearch,repairCount),0,2);
        repairCount.MinimumSize = new(0,UiPalette.FieldHeight); repairCount.TextAlign = ContentAlignment.MiddleLeft;
        repairsPage.Controls.Add(repairActionsList,0,3);
    }
    private void AddRepairAction(RoundedButton button,string description,string category)
    {
        button.AutoSize = false; button.MinimumSize = new(0,UiPalette.FieldHeight);
        repairActionsList.Add(button,description,category);
    }
    private Task RunRepairDefinitionAsync(RepairDefinition definition) => RepairAsync(definition.Title,[definition.Id],
        (definition.ReadOnly ? "Run this read-only diagnostic?" : "Run this Windows maintenance action?")+"\n\n"+definition.Description);
    private Task RunRepairDefinitionAsync(string id) => RunRepairDefinitionAsync(RepairCatalog.All.Single(definition => definition.Id == id));
    private void RefreshRepairFilter()
    {
        repairActionsList.Filter(repairSearch.Text);
        repairCount.Text = $"{repairActionsList.ShownCount} / {repairActionsList.EntryCount} actions • Run one from its button";
    }
    internal void SetRepairLayoutCheck(string query) => repairSearch.Text = query;
    private IEnumerable<string> VerifyMaintenancePageExtent()
    {
        if (!repairsPage.Visible) yield break;
        int expected = Math.Max(repairsViewport.Viewport.Height,repairsPage.GetPreferredSize(new(repairsPage.Width,0)).Height);
        if (repairsPage.Height > expected+1) yield return "Repairs retains a stale scroll extent.";
        if (repairsPage.Height > repairsViewport.Viewport.Height)
        {
            int bottom = repairActionsList.Top+repairActionsList.GetPreferredSize(new(repairActionsList.Width,0)).Height;
            if (repairsPage.Height-bottom > LogicalToDeviceUnits(UiPalette.SectionGap)) yield return "Repairs has scrollable blank space after its final action.";
        }
    }
    private IEnumerable<string> VerifyMaintenanceFixtures()
    {
        if (CleanupCatalog.All.Count != 22) yield return "The fixed cleanup area inventory is incomplete.";
        if (Theme.Descendants(cleanupPage).Any(control => control is DetailListPanel || control.Name.Contains("cleanupArea_",StringComparison.Ordinal)
            || control.Name.Contains("customizeCleanup",StringComparison.Ordinal)))
            yield return "Cleanup still creates area-selection controls.";
        if (Theme.Descendants(repairsPage).OfType<DataGridView>().Any()) yield return "Repairs still uses a selection grid.";
        if (Theme.Descendants(repairsPage).OfType<DarkScrollPanel>().Any()) yield return "Repairs still contains a nested page scrollbar.";
        string query = repairSearch.Text;
        try
        {
            repairSearch.Text = "Winsock";
            if (repairActionsList.ShownCount != 2) yield return "Repair button search loses the Winsock actions.";
            repairSearch.Text = "no-matching-action-fixture";
            if (repairActionsList.ShownCount != 0) yield return "Repair button search retains unrelated actions.";
        }
        finally { repairSearch.Text = query; }
    }
}
