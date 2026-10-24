using BoosterX.App.Controls;
using BoosterX.App.Services;
using BoosterX.Core;

namespace BoosterX.App;

public partial class Form1
{
    private readonly List<OptimizationRow> preferenceInventory = [];
    private readonly List<OptionalAppRow> appInventory = [];
    private readonly DarkSearchBox preferenceSearch = new() { Name = "preferenceSearch",PlaceholderText = "Search preferences…" };
    private readonly DarkSearchBox appsSearch = new() { Name = "appsSearch",PlaceholderText = "Search installed apps…" };
    private readonly DarkSearchBox repairSearch = new() { Name = "repairSearch",PlaceholderText = "Search maintenance actions…" };
    private readonly DarkComboBox preferenceCategory = new() { Name = "preferenceCategory",Size = new(190,40),Symbol = UiIcon.Checklist };
    private readonly DarkCheckBox removableOnly = new() { Name = "removableOnly",Text = "Removable only",AutoSize = true };
    private readonly Label preferenceCount = new() { Name = "preferenceCount",AutoSize = true,Tag = "muted" };
    private readonly Label appsCount = new() { Name = "appsCount",AutoSize = true,Tag = "muted" };
    private readonly Label repairCount = new() { Name = "repairCount",AutoSize = true,Tag = "muted" };
    private readonly Label operationPercentLabel = new() { Name = "operationPercentLabel",AutoSize = true,Text = "0%",TextAlign = ContentAlignment.MiddleRight,Anchor = AnchorStyles.Right };
    private readonly OperationProgress workProgress = new();
    private readonly object progressLock = new();
    private double latestNativePercent;
    private bool nativePercentQueued, reportingUiException;
    private long operationGeneration;
    private string historyProblemSignature = "";

    private void InitializeFeatureControls()
    {
        var inspect = ActionButton("inspectPreferencesButton","Inspect current values",UiIcon.Search);
        inspect.Click += async (_,_) => await GuardAsync(InspectPreferencesAsync);
        var filters = Flow("preferenceFilters",preferenceSearch,preferenceCategory,inspect);
        InsertPageRow(optimizePage,2,filters); InsertPageRow(optimizePage,3,preferenceCount);
        preferenceCategory.Items.AddRange(new object[] { "All categories" }.Concat(OptimizationCatalog.All.Select(p => p.Category).Distinct().Order().Cast<object>()).ToArray());
        preferenceCategory.SelectedIndex = 0;
        preferenceSearch.TextChanged += (_,_) => RefreshPreferenceFilter(); preferenceCategory.SelectedIndexChanged += (_,_) => RefreshPreferenceFilter();
        optimizationGrid.Columns.Insert(2,TextColumn("preferenceCategoryColumn","Category","Category",120));
        optimizationGrid.Columns.Add(TextColumn("preferenceStateColumn","Current values","State",145));

        InitializeCleanupControls();

        InsertPageRow(appsPage,2,Flow("appsFilters",appsSearch,removableOnly)); InsertPageRow(appsPage,3,appsCount);
        appsSearch.TextChanged += (_,_) => RefreshAppFilter(); removableOnly.CheckedChanged += (_,_) => RefreshAppFilter();
        findAppsButton.Text = "Scan installed apps"; storeAppButton.Text = "Store / app settings";
        var appDetails = ActionButton("installedAppDetailsButton","App details",UiIcon.Info);
        appDetails.Click += async (_,_) => await GuardReadOnlyAsync(() =>
        {
            if (appsGrid.CurrentRow?.DataBoundItem is not OptionalAppRow row) throw new InvalidOperationException("Select an installed application first.");
            OptionalApp app = row.Item;
            DarkDialog.Show(this,$"{app.Name}\nVersion: {app.Version}\nPublisher: {app.Publisher}\nType: {app.Kind}\nScope: {app.Scope}\n\n{app.Protection}\n\nIdentity: {app.FullName}\nInstall location: {app.InstallLocation}\nRegistered uninstall command: {app.UninstallString}","Installed application details");
            return Task.CompletedTask;
        });
        appsActions.Controls.Add(appDetails);
        appsTitleLabel.Text = "Review installed applications";
        appsDescriptionLabel.Text = "Inventory current-user AppX packages and registered Win32/MSI apps. Review type, scope and protection. Publisher uninstallers may keep shared dependencies or personal profiles; nothing is selected automatically.";
        appsGrid.Columns.Clear(); AddSelectionColumn(appsGrid);
        appsGrid.Columns.Add(TextColumn("installedName","Application","Name",220));
        appsGrid.Columns.Add(TextColumn("installedType","Type","Type",90));
        appsGrid.Columns.Add(TextColumn("installedScope","Scope","Scope",110));
        appsGrid.Columns.Add(TextColumn("installedVersion","Version","Version",110));
        appsGrid.Columns.Add(TextColumn("installedProtection","Protection / removal behavior","Protection",370));
        appsCount.Text = "Use Scan installed apps to load this PC's inventory.";

        InitializeRepairControls();
        foreach (OptimizationDefinition definition in OptimizationCatalog.All)
        {
            var row = new OptimizationRow(definition); row.PropertyChanged += (_,_) => RefreshPreferenceCount(); preferenceInventory.Add(row);
        }
        RefreshPreferenceFilter(); RefreshRepairFilter();
        historyGrid.Columns.Add(TextColumn("historyIdentity","Backup scope","Scope",155));
        footerLayout.ColumnCount = 3; footerLayout.ColumnStyles.Clear();
        footerLayout.ColumnStyles.Add(new(SizeType.Percent,100)); footerLayout.ColumnStyles.Add(new(SizeType.AutoSize)); footerLayout.ColumnStyles.Add(new(SizeType.AutoSize));
        footerLayout.SetColumn(stopButton,2); footerLayout.Controls.Add(operationPercentLabel,1,0); footerLayout.SetColumnSpan(operationProgress,3);
        help.SetToolTip(operationPercentLabel,"Completed actions plus supported native command/package progress. This is work progress, not an estimate of remaining time; unknown phases use animation.");
        NormalizeSpacing();
    }
    private static RoundedButton ActionButton(string name,string text,UiIcon icon) => new()
    { Name = name,Text = text,Symbol = icon,AutoSize = true,AutoSizeMode = AutoSizeMode.GrowAndShrink,Padding = new(12,7,12,7),MinimumSize = new(130,40) };
    private static BufferedFlowLayoutPanel Flow(string name,params Control[] controls)
    {
        var flow = new BufferedFlowLayoutPanel { Name = name,Dock = DockStyle.Fill,AutoSize = true,AutoSizeMode = AutoSizeMode.GrowAndShrink,WrapContents = true,Margin = Padding.Empty };
        flow.Controls.AddRange(controls); return flow;
    }
    private static DataGridViewTextBoxColumn TextColumn(string name,string title,string property,int width) => new()
    { Name = name,HeaderText = title,DataPropertyName = property,ReadOnly = true,Width = width,MinimumWidth = Math.Min(width,90),Tag = new Size(width,Math.Min(width,90)),SortMode = DataGridViewColumnSortMode.NotSortable };
    private static void AddSelectionColumn(DataGridView grid) => grid.Columns.Add(new DataGridViewCheckBoxColumn
    { Name = grid.Name + "Select",HeaderText = "Select",DataPropertyName = "Selected",Width = 56,MinimumWidth = 56,Tag = new Size(56,56),SortMode = DataGridViewColumnSortMode.NotSortable });
    private static void InsertPageRow(TableLayoutPanel page,int index,Control control)
    {
        page.SuspendLayout();
        try
        {
            page.RowCount++;
            foreach (Control child in page.Controls.Cast<Control>().OrderByDescending(page.GetRow).ToArray())
                if (page.GetRow(child) >= index) page.SetRow(child,page.GetRow(child)+1);
            page.RowStyles.Insert(index,new RowStyle(SizeType.AutoSize)); page.Controls.Add(control,0,index);
        }
        finally { page.ResumeLayout(true); }
    }
    private void NormalizeSpacing()
    {
        foreach (TableLayoutPanel page in Theme.Descendants(pageHost).OfType<TableLayoutPanel>().Where(table => table.Name.EndsWith("Page",StringComparison.Ordinal)))
        {
            page.Margin = Padding.Empty; page.Padding = Padding.Empty;
            foreach (Control child in page.Controls) child.Margin = new(0,0,0,UiPalette.SectionGap);
        }
        foreach (FlowLayoutPanel flow in Theme.Descendants(pageHost).OfType<FlowLayoutPanel>())
        {
            flow.Padding = Padding.Empty;
            flow.Margin = new(0,0,0,UiPalette.SectionGap-UiPalette.ActionGap);
            foreach (Control child in flow.Controls)
            {
                child.Margin = new(0,0,UiPalette.ActionGap,UiPalette.ActionGap);
                if (child is DarkCheckBox check) check.MinimumSize = new(0,UiPalette.FieldHeight);
            }
        }
        foreach (DarkScrollPanel viewport in Theme.Descendants(pageHost).OfType<DarkScrollPanel>()) viewport.Margin = Padding.Empty;
        foreach (DarkGridHost host in Theme.Descendants(pageHost).OfType<DarkGridHost>())
            host.Margin = host.Parent is TableLayoutPanel table && table.GetRow(host) < table.RowCount-1 ? new(0,0,0,UiPalette.SectionGap) : Padding.Empty;
        activitySearchBox.Size = new(230,UiPalette.FieldHeight);
        operationPercentLabel.Margin = new(8,0,12,4);
    }
    private void RefreshPreferenceFilter()
    {
        string query = preferenceSearch.Text.Trim(); string category = preferenceCategory.SelectedItem as string ?? "All categories";
        optimizations.ReplaceWith(preferenceInventory.Where(row => (category == "All categories" || row.Category == category)
            && (query.Length == 0 || (row.Title+" "+row.Description+" "+row.Category).Contains(query,StringComparison.OrdinalIgnoreCase))));
        RefreshPreferenceCount();
    }
    private void RefreshPreferenceCount() => preferenceCount.Text = $"{optimizations.Count} shown / {preferenceInventory.Count} preferences  •  {preferenceInventory.Count(row => row.Selected)} selected across all filters";
    private void RefreshAppFilter()
    {
        string query = appsSearch.Text.Trim();
        optionalApps.ReplaceWith(appInventory.Where(row => (!removableOnly.Checked || row.CanSelect)
            && (query.Length == 0 || (row.Name+" "+row.Item.Publisher+" "+row.PackageName).Contains(query,StringComparison.OrdinalIgnoreCase))));
        appsCount.Text = $"{optionalApps.Count} shown / {appInventory.Count} installed entries  •  {appInventory.Count(row => row.Selected)} selected  •  {appInventory.Count(row => !row.CanSelect)} protected";
    }
    private async Task InspectPreferencesAsync()
    {
        await RunBusyAsync("Inspect preference values",async token =>
        {
            var values = await Task.Run(() => preferenceInventory.Select(row =>
            {
                token.ThrowIfCancellationRequested();
                if (!row.CanSelect) return (row,"Unsupported build");
                if (row.Item.RegistryWrites == null) return (row,"Inspect in Overview / History");
                try
                {
                    bool same = row.Item.RegistryWrites(options).All(write =>
                    {
                        RegistrySnapshot actual = RegistryService.Capture(write.Target);
                        return actual.Exists && actual.Kind == write.Kind && actual.Value.GetRawText() == JsonDefaults.Element(write.Value).GetRawText();
                    });
                    return (row,same ? "Requested values present" : "Different / default values");
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException) { return (row,"Could not read: " + ex.Message); }
            }).ToArray(),token);
            foreach ((OptimizationRow row,string state) in values) row.State = state;
            return "Preference values inspected. Registry values alone do not measure a performance improvement.";
        },notify:false);
    }
    private void RefreshProgressDisplay()
    {
        operationPercentLabel.Text = workProgress.Caption;
        operationProgress.Style = workProgress.IsIndeterminate ? ProgressBarStyle.Marquee : ProgressBarStyle.Continuous;
        operationProgress.Maximum = 1000; operationProgress.Value = (int)Math.Clamp(Math.Round((workProgress.Percent ?? 0)*10),0,1000);
    }
    private void ReportRunProgress(RunProgress progress)
    {
        if (!busy || IsDisposed || resourcesClosed) return;
        workProgress.SetSteps(progress.Completed,progress.Total); RefreshProgressDisplay(); SetStatus(progress.Message); WriteLog(progress.Message);
    }
    private void ReportNativePercent(double value)
    {
        if (IsDisposed || Disposing || !IsHandleCreated) return;
        long generation = Interlocked.Read(ref operationGeneration);
        lock (progressLock) { latestNativePercent = value; if (nativePercentQueued) return; nativePercentQueued = true; }
        BeginInvoke((Action)(() =>
        {
            double latest; lock (progressLock) { latest = latestNativePercent; nativePercentQueued = false; }
            if (!busy || generation != operationGeneration || IsDisposed || resourcesClosed) return;
            workProgress.SetStepPercent(latest); RefreshProgressDisplay();
        }));
    }
    internal void ReportUnexpectedUiException(Exception exception)
    {
        if (reportingUiException || IsDisposed || resourcesClosed) return;
        reportingUiException = true;
        try
        {
            WriteActivity(exception.ToString(),ActivityLevel.Error); SetStatus("Interface action failed. Open Activity for full details.");
            DarkDialog.Show(this,exception.Message + "\n\nFull details were recorded in Activity and the session log.","BoosterX interface",MessageBoxButtons.OK,MessageBoxIcon.Error);
        }
        finally { reportingUiException = false; }
    }
    internal IReadOnlyList<string> VerifyFeatureFixtures()
    {
        var issues = new List<string>();
        if (preferenceInventory.Count != 34 || CleanupCatalog.All.Count != 22 || repairActionsList.EntryCount != RepairCatalog.All.Count+5) issues.Add("Expanded feature inventories are incomplete.");
        OptimizationRow selected = preferenceInventory.Single(row => row.Item.Id == "mouse-acceleration");
        bool original = selected.Selected; string query = preferenceSearch.Text; int category = preferenceCategory.SelectedIndex;
        try
        {
            preferenceCategory.SelectedIndex = 0; selected.Selected = true; preferenceSearch.Text = "Faster menus";
            if (optimizations.Count != 1 || !selected.Selected) issues.Add("Filtering loses hidden selected preferences.");
        }
        finally { preferenceSearch.Text = query; preferenceCategory.SelectedIndex = category; selected.Selected = original; }
        var protectedApp = new OptionalAppRow(new OptionalApp("Framework fixture","fixture","fixture","fixture","1",Protected:true));
        protectedApp.Selected = true; if (protectedApp.Selected) issues.Add("Protected installed applications can be selected.");
        issues.AddRange(VerifyMaintenanceFixtures());
        return issues;
    }
    internal IEnumerable<string> VerifyPageAlignment()
    {
        foreach (TableLayoutPanel page in Theme.Descendants(pageHost).OfType<TableLayoutPanel>().Where(table => table.Visible && table.Name.EndsWith("Page",StringComparison.Ordinal) && table.ColumnCount == 1))
        {
            int expected = page.Padding.Left;
            foreach (Control child in page.Controls)
                if (child.Visible && child.Left != expected) yield return page.Name + ": " + child.Name + " has a different left edge.";
        }
        foreach (FlowLayoutPanel flow in Theme.Descendants(pageHost).OfType<FlowLayoutPanel>().Where(control => control.Visible))
        {
            var fields = flow.Controls.Cast<Control>().Where(control => control.Visible && control is DarkSearchBox or DarkComboBox).ToArray();
            if (fields.Length > 1 && fields.Select(control => control.Height).Distinct().Count() != 1) yield return flow.Name + " uses inconsistent field heights.";
        }
        foreach (DetailListPanel list in Theme.Descendants(pageHost).OfType<DetailListPanel>().Where(list => list.Visible))
            foreach (string issue in list.VerifyRows()) yield return issue;
        foreach (string issue in VerifyMaintenancePageExtent()) yield return issue;
    }
}
