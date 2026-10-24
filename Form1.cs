using BoosterX.App.Controls;
using System.Collections.Concurrent;
using System.ComponentModel;
using System.Diagnostics;
using System.Text;
using BoosterX.App.Services;
using BoosterX.Core;

namespace BoosterX.App;

public partial class Form1 : DarkForm
{
    // Runtime defaults are configurable in BoosterOptions.cs / settings.json; UI colors and limits are in Theme.cs.
    private BoosterOptions options = new();
    private string dataDirectory = "";
    private string logPath = "";
    private string machineId = "";
    private string userSid = "";
    private RunStore store = null!;
    private WindowsOperations operations = null!;
    private ChangeEngine engine = null!;
    private TextWriter logFile = TextWriter.Null;
    private readonly ConcurrentQueue<ActivityEntry> logQueue = new();
    private readonly List<ActivityEntry> activityEntries = [];
    private readonly BulkBindingList<ActivityEntry> activityRows = [];
    private UiPreferences uiPreferences = new();
    private NotifyIcon? desktopIcon;
    private ContextMenuStrip? desktopMenu;
    private Icon? ownedIcon;
    private Bitmap? brandImage;
    private long activitySequence;
    private int activityCharacters;
    private bool updatingActivity;
    private readonly BulkBindingList<OptimizationRow> optimizations = [];
    private readonly BulkBindingList<TempRow> temporaryFiles = [];
    private readonly BulkBindingList<OptionalAppRow> optionalApps = [];
    private readonly BulkBindingList<StartupRow> startupItems = [];
    private readonly BulkBindingList<HistoryRow> history = [];
    private readonly ToolTip help = new();
    private CancellationTokenSource? cancellation;
    private bool runtimeReady;
    private bool busy;
    private ActivityLevel operationOutcome;
    private bool resourcesClosed;
    private int logFailureReported;
    private readonly bool layoutCheckOnly;

    /// <summary>The designer constructor builds controls only; it never accesses or changes Windows settings.</summary>
    public Form1()
    {
        InitializeComponent();
        InitializeFeatureControls();
        InitializeActionIcons();
        ownedIcon = AppAssets.LoadIcon();
        Icon = ownedIcon;
        ShowIcon = true;
        brandImage = AppAssets.LoadBrandImage();
        brandIconBox.Image = brandImage;
        activityGrid.DataSource = activityRows;
        Theme.Apply(this);
        Theme.StyleButton(overviewNavButton, active: true);
        AttachTitleDrag(headerLayout, brandLayout, brandLabel, brandSubtitleLabel, brandIconBox, headerToolsFlow, captionButtonsFlow);
        foreach (DataGridView grid in Theme.Descendants(pageHost).OfType<DataGridView>()) grid.CellPainting += PaintGridCheckBox;
        help.OwnerDraw = true;
        help.BackColor = UiPalette.Surface;
        help.ForeColor = UiPalette.Text;
        help.Popup += (_, e) =>
        {
            string text = help.GetToolTip(e.AssociatedControl) ?? "";
            Size measured = TextRenderer.MeasureText(text, Font, new Size(LogicalToDeviceUnits(480), int.MaxValue), TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix);
            e.ToolTipSize = new Size(measured.Width + LogicalToDeviceUnits(20), measured.Height + LogicalToDeviceUnits(16));
        };
        help.Draw += (_, e) =>
        {
            using var brush = new SolidBrush(UiPalette.Surface);
            using var border = new Pen(UiPalette.Border);
            e.Graphics.FillRectangle(brush, e.Bounds);
            e.Graphics.DrawRectangle(border, 0, 0, e.Bounds.Width - 1, e.Bounds.Height - 1);
            TextRenderer.DrawText(e.Graphics, e.ToolTipText, Font, Rectangle.Inflate(e.Bounds, -LogicalToDeviceUnits(10), -LogicalToDeviceUnits(8)),
                UiPalette.Text, TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix);
        };
        help.SetToolTip(minimizeWindowButton, "Minimize window");
        help.SetToolTip(maximizeWindowButton, "Maximize or restore window");
        help.SetToolTip(closeWindowButton, "Close BoosterX");
    }

    public Form1(BoosterOptions options, string dataDirectory) : this()
    {
        this.options = options;
        this.dataDirectory = dataDirectory;
    }

    internal Form1(bool layoutCheckOnly) : this() { this.layoutCheckOnly = layoutCheckOnly; }

    protected override async void OnShown(EventArgs e)
    {
        base.OnShown(e);
        if (layoutCheckOnly || DesignMode || LicenseManager.UsageMode == LicenseUsageMode.Designtime) return;
        try
        {
            InitializeRuntime();
            await RunBusyAsync("Read system information", async _ =>
            {
                await RefreshOverviewAsync();
                await RefreshAdaptersAsync();
                RefreshHistory();
                WriteLog("Account: " + System.Security.Principal.WindowsIdentity.GetCurrent().Name);
                WriteLog("Startup is read only. No optimization is applied automatically.");
                return ElevationService.IsAdministrator ? "Ready. Review a plan before applying changes."
                    : "Ready. Analyze and review normally; applying changes requires administrator rights.";
            }, notify: false);
        }
        catch (Exception ex)
        {
            DarkDialog.Show(this, ex.Message, "BoosterX startup", MessageBoxButtons.OK, MessageBoxIcon.Error);
            Close();
        }
    }

    private void InitializeRuntime()
    {
        if (string.IsNullOrEmpty(dataDirectory))
        {
            dataDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "BoosterX");
            Directory.CreateDirectory(dataDirectory);
            options = BoosterOptions.LoadOrCreate(Path.Combine(dataDirectory, "settings.json"));
        }
        string? preferenceWarning = null;
        try { uiPreferences = UiPreferences.Load(dataDirectory); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Text.Json.JsonException)
        { uiPreferences = new(); preferenceWarning = "UI preferences could not be read; using defaults. " + ex.Message; }
        followActivityCheckBox.Checked = uiPreferences.FollowActivity;
        notificationsCheckBox.Checked = uiPreferences.DesktopNotifications;
        foregroundNotificationsCheckBox.Checked = uiPreferences.NotifyInForeground;
        InitializeDesktopNotifications();
        machineId = DiagnosticsService.MachineId;
        userSid = DiagnosticsService.UserSid;
        store = new RunStore(Path.Combine(dataDirectory, "Backups"));
        string logs = Path.Combine(dataDirectory, "Logs");
        Directory.CreateDirectory(logs);
        logPath = Path.Combine(logs, $"BoosterX-{DateTime.Now:yyyyMMdd-HHmmss}-{Guid.NewGuid():N}.log");
        logFile = TextWriter.Synchronized(new StreamWriter(logPath, append: false, Encoding.UTF8) { AutoFlush = true });
        if (preferenceWarning != null) WriteActivity(preferenceWarning, ActivityLevel.Warning);
        operations = new WindowsOperations(options, WriteLog,ReportNativePercent);
        engine = new ChangeEngine(store, operations, machineId, userSid);
        optimizationGrid.DataSource = optimizations;
        cleanupGrid.DataSource = temporaryFiles;
        appsGrid.DataSource = optionalApps;
        startupGrid.DataSource = startupItems;
        historyGrid.DataSource = history;
        RefreshPreferenceFilter();
        automaticPointCheckBox.Checked = options.AutomaticRestorePoint;
        cleanupInfoLabel.Text = $"Minimum age: {options.TempMinimumAgeDays} days since BOTH creation and modification. Scan before deleting.";
        dnsComboBox.DataSource = NetworkService.Presets.ToList();
        dnsComboBox.SelectedIndex = -1;
        foreach (DataGridView grid in new[] { optimizationGrid, cleanupGrid, appsGrid, startupGrid })
        {
            grid.CurrentCellDirtyStateChanged += SelectionGrid_CurrentCellDirtyStateChanged;
            grid.CellBeginEdit += SelectionGrid_CellBeginEdit;
            grid.DataError += SelectionGrid_DataError;
        }
        Theme.Apply(this);
        ApplyResponsiveLayout();
        elevateButton.Text = ElevationService.IsAdministrator ? "Administrator active" : "Run as administrator";
        elevateButton.Enabled = !ElevationService.IsAdministrator;
        help.SetToolTip(elevateButton, "Normal launch supports dotnet run. Elevation restarts the application; selections are reviewed again afterward.");
        help.SetToolTip(automaticPointCheckBox, "Windows may throttle or reject restore-point creation. Per-step setting backups remain separate.");
        help.SetToolTip(logToggle, "Open the activity list, filter events, read full details or export the session log.");
        runtimeReady = true;
        activityTimer.Interval = Theme.LogRefreshMilliseconds;
        activityTimer.Start();
        ShowPage(overviewPage, overviewNavButton);
    }

    private void ShowPage(Control page, Button activeButton)
    {
        pageHost.SuspendLayout();
        try
        {
            Control viewport = page.Parent?.Parent is DarkScrollPanel host ? host : page;
            foreach (Control child in pageHost.Controls) child.Visible = ReferenceEquals(child, viewport);
            viewport.BringToFront();
            Theme.StyleButton(logToggle, ReferenceEquals(page, activityPage));
            foreach (Button button in navigationPanel.Controls.OfType<Button>()) Theme.StyleButton(button, ReferenceEquals(button, activeButton));
        }
        finally { pageHost.ResumeLayout(performLayout: true); }
    }

    internal void PrepareLayoutCheck()
    {
        // Isolated visual fixtures only. No Windows services, files, registry values or installed apps are modified.
        Text = "BoosterX UI self-check - fixture data, no optimizations applied";
        optimizationGrid.DataSource = optimizations;
        RefreshPreferenceFilter();
        windowsLabel.Text = "Windows layout verification — fixture data";
        processorLabel.Text = "CPU  Long processor name used to exercise automatic wrapping and DPI scaling";
        memoryLabel.Text = "RAM  32.0 GB total / 20.0 GB available (fixture)";
        diskLabel.Text = "DISK  C:\\ 150.0 GB free / 500.0 GB total (fixture)";
        powerLabel.Text = "POWER  Current plan: 01234567-89ab-cdef-0123-456789abcdef (fixture)";
        accountLabel.Text = "ACCOUNT  EXAMPLE\\AdministratorWithALongAccountName (fixture)";
        ApplyResponsiveLayout(); AdjustComboHeights(); ScaleGridColumns();
        for (int i = 0; i < 80; i++)
            logQueue.Enqueue(new ActivityEntry(i + 1, DateTimeOffset.Now, (ActivityLevel)(i % 4),
                $"Fixture event {i + 1}: a long description to exercise the activity list and dark scrollbars.\nComplete details, line 2.\nLine 3."));
        reportTextBox.Text = string.Join("\n", Enumerable.Range(1, 100).Select(i => $"Diagnostic fixture line {i}: CPU, RAM and Windows status."));
        appsGrid.DataSource = optionalApps;
        optionalApps.ReplaceWith(new[]
        {
            new OptionalAppRow(new OptionalApp("Example package","Example.Package","fixture-appx","Example.Package_family","1.0")),
            new OptionalAppRow(new OptionalApp("Shared runtime fixture","fixture-runtime","fixture-runtime","","10.0",InstalledAppKind.Desktop,"All users",Protected:true,Protection:"Shared runtime"))
        });
        cleanupGrid.DataSource = temporaryFiles;
        temporaryFiles.ReplaceWith(Enumerable.Range(1,30).Select(i => new TempRow(new TempCandidate(@"C:\Fixture\Temp",@"C:\Fixture\Temp\cached-file-"+i+".tmp",1024*i,DateTime.UtcNow.AddDays(-14),DateTime.UtcNow.AddDays(-20),"Fixture cache area"))));
        workProgress.SetSteps(1,4); workProgress.SetStepPercent(50); RefreshProgressDisplay();
        FlushLog();
    }

    internal IEnumerable<string> LayoutPageNames => new[] { "overview", "optimize", "cleanup", "apps", "startup", "network", "repairs", "activity", "history" };
    internal void SelectLayoutPage(string name)
    {
        Control page = Theme.Descendants(pageHost).FirstOrDefault(c => c.Name == name + "Page") ?? throw new ArgumentException("Unknown page.", nameof(name));
        Button button = navigationPanel.Controls[name + "NavButton"] as Button ?? throw new ArgumentException("Unknown navigation item.", nameof(name));
        ShowPage(page, button);
    }

    private async Task GuardAsync(Func<Task> action)
    {
        if (!runtimeReady || busy) return;
        try { await action(); }
        catch (Exception ex)
        {
            WriteActivity(ex.ToString(), ActivityLevel.Error);
            SetStatus(ex.Message);
            DarkDialog.Show(this, ex.Message, "BoosterX", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private async Task<bool> RunBusyAsync(string title, Func<CancellationToken, Task<string>> action, bool notify = true)
    {
        if (busy) return false;
        busy = true;
        Interlocked.Increment(ref operationGeneration); workProgress.Reset(); RefreshProgressDisplay();
        operationOutcome = ActivityLevel.Success;
        SetPageActionsEnabled(false);
        elevateButton.Enabled = false;
        stopButton.Enabled = true;
        cancellation?.Dispose();
        cancellation = new CancellationTokenSource();
        operationProgress.Style = ProgressBarStyle.Marquee;
        operationProgress.MarqueeAnimationSpeed = 30;
        SetStatus(title + "…");
        WriteLog(title);
        try
        {
            string message = await action(cancellation.Token);
            if (!cancellation.IsCancellationRequested) { workProgress.Complete(); RefreshProgressDisplay(); }
            SetStatus(message); WriteActivity(message, operationOutcome);
            if (notify) NotifyDesktop(title, message, operationOutcome == ActivityLevel.Error ? ToolTipIcon.Error
                : operationOutcome == ActivityLevel.Warning ? ToolTipIcon.Warning : ToolTipIcon.Info);
            return true;
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
            SetStatus("Stopped. Completed changes remain recorded in History.");
            WriteActivity("Stop completed.", ActivityLevel.Warning);
            if (notify) NotifyDesktop(title, "Stopped. Completed changes remain in History.", ToolTipIcon.Warning);
            return false;
        }
        catch (Exception ex)
        {
            WriteActivity(ex.ToString(), ActivityLevel.Error); SetStatus("Action failed. Review History and the activity log.");
            if (notify) NotifyDesktop(title, "Action failed. Open Activity for details.", ToolTipIcon.Error);
            FlushLog();
            ShowPage(activityPage, activityNavButton);
            DarkDialog.Show(this, ex.Message, title, MessageBoxButtons.OK, MessageBoxIcon.Error);
            return false;
        }
        finally
        {
            operationProgress.Style = ProgressBarStyle.Continuous;
            operationPercentLabel.Text = workProgress.Percent.HasValue ? workProgress.Caption : "—";
            SetPageActionsEnabled(true);
            elevateButton.Enabled = !ElevationService.IsAdministrator;
            stopButton.Enabled = false;
            busy = false;
            try { RefreshHistory(); } catch (Exception ex) { WriteLog("History could not be read: " + ex.Message); }
        }
    }

    private void SetPageActionsEnabled(bool enabled)
    {
        // Keep reports and tables readable while a batch runs; disable actions, not the entire visual tree.
        foreach (Control control in Theme.Descendants(pageHost))
            if ((control is Button or CheckBox or DarkComboBox) && !activityPage.Contains(control)) control.Enabled = enabled;
        foreach (DataGridView grid in Theme.Descendants(pageHost).OfType<DataGridView>()) grid.Invalidate();
    }

    private void PaintGridCheckBox(object? sender, DataGridViewCellPaintingEventArgs e)
    {
        if (sender is not DataGridView grid || e.RowIndex < 0 || e.ColumnIndex < 0
            || grid.Columns[e.ColumnIndex] is not DataGridViewCheckBoxColumn || e.Graphics == null) return;
        e.PaintBackground(e.ClipBounds, true);
        e.Paint(e.ClipBounds, DataGridViewPaintParts.Border);
        int size = grid.LogicalToDeviceUnits(16);
        Rectangle box = new(e.CellBounds.Left + (e.CellBounds.Width - size) / 2,
            e.CellBounds.Top + (e.CellBounds.Height - size) / 2, size, size);
        bool enabled = !busy && grid.Rows[e.RowIndex].DataBoundItem is not ISelectableRow { CanSelect: false };
        CheckState state = e.Value is bool value && value ? CheckState.Checked : CheckState.Unchecked;
        UiDrawing.Check(e.Graphics, box, state, enabled);
        e.Handled = true;
    }

    private void MinimizeWindowButton_Click(object? sender, EventArgs e) => WindowState = FormWindowState.Minimized;
    private void MaximizeWindowButton_Click(object? sender, EventArgs e) => ToggleMaximize();
    private void CloseWindowButton_Click(object? sender, EventArgs e) => Close();
    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        UpdateResponsiveWindowState();
        if (maximizeWindowButton != null)
        {
            maximizeWindowButton.Glyph = WindowState == FormWindowState.Maximized ? CaptionGlyph.Restore : CaptionGlyph.Maximize;
            maximizeWindowButton.AccessibleName = WindowState == FormWindowState.Maximized ? "Restore window" : "Maximize window";
        }
    }

    private bool EnsureAdministrator()
    {
        if (ElevationService.IsAdministrator) return true;
        DialogResult result = DarkDialog.Show(this,
            "This action requires administrator rights. Restart BoosterX as administrator now?\n\nReview and re-select your actions after restart. User-scoped actions affect the account used for elevation.",
            "Administrator required", MessageBoxButtons.YesNo, MessageBoxIcon.Information, MessageBoxDefaultButton.Button2);
        if (result == DialogResult.Yes) RestartElevated();
        return false;
    }

    private void RestartElevated()
    {
        if (busy || ElevationService.IsAdministrator) return;
        if (ElevationService.TryRestart()) Close();
        else { SetStatus("Elevation cancelled. No changes were applied."); WriteLog("Elevation cancelled."); }
    }

    private async Task<bool> ApplyPlanAsync(string title, IReadOnlyList<IChangeStep> plan, string? confirmation = null)
    {
        if (plan.Count == 0) throw new InvalidOperationException("Select at least one action first.");
        if (!EnsureAdministrator()) return false;
        string message = (confirmation ?? "Apply these changes? Original settings are backed up before modification.")
            + "\n\n" + string.Join("\n", plan.Take(18).Select(s => "• " + s.Title))
            + (plan.Count > 18 ? $"\n… and {plan.Count - 18} more selected actions." : "")
            + (plan.Any(s => !s.CanUndo) ? "\n\nThis batch includes actions without a BoosterX undo operation." : "");
        if (DarkDialog.Show(this, message, title, MessageBoxButtons.OKCancel, MessageBoxIcon.Information,
                MessageBoxDefaultButton.Button2) != DialogResult.OK) return false;
        bool restorePoint = automaticPointCheckBox.Checked && plan.Any(s => s.CanUndo || s.Kind is "appx" or "windows-repair");
        return await RunBusyAsync(title, async token =>
        {
            if (restorePoint) { WriteLog("Requesting Windows restore point…"); WriteLog(await Task.Run(RestorePointService.TryCreate)); }
            var reporter = new Progress<RunProgress>(ReportRunProgress);
            ChangeRun run = await Task.Run(() => engine.RunAsync(title, plan, reporter, token));
            operationOutcome = run.HasErrors ? ActivityLevel.Error : run.WasCancelled ? ActivityLevel.Warning : ActivityLevel.Success;
            foreach (ChangeEntry entry in run.Entries)
                WriteActivity($"[{entry.Status}] {entry.Title}: {entry.Message}", entry.Status switch
                {
                    EntryStatus.Failed or EntryStatus.RestoreFailed or EntryStatus.RecoveryRequired => ActivityLevel.Error,
                    EntryStatus.RolledBack => ActivityLevel.Warning,
                    EntryStatus.Applied or EntryStatus.Restored => ActivityLevel.Success,
                    _ => ActivityLevel.Info
                });
            await RefreshOverviewAsync(); RefreshHistory();
            string summary = run.HasErrors ? "Completed with errors. Review per-action results in History."
                : run.WasCancelled ? "Stopped after the current action. Completed changes remain in History."
                : "Batch complete. Review per-action results in History.";
            if (run.Entries.Any(entry => entry.RestartRequired)) summary += " Restart Windows when convenient.";
            return summary;
        });
    }

    private async Task RefreshOverviewAsync()
    {
        SystemOverview info = await Task.Run(DiagnosticsService.Overview);
        windowsLabel.Text = info.Windows + "  |  Uptime: " + info.Uptime + "  |  Restart: " + (info.RestartPending ? "pending" : "not detected");
        processorLabel.Text = "CPU  " + info.Processor;
        memoryLabel.Text = "RAM  " + info.Memory;
        diskLabel.Text = "DISK  " + info.Disk;
        powerLabel.Text = "POWER  " + info.Power;
        accountLabel.Text = "ACCOUNT  " + info.Account + (ElevationService.IsAdministrator ? " (administrator)" : " (standard user)");
    }

    private async Task RefreshAdaptersAsync()
    {
        string? selected = (adapterComboBox.SelectedItem as AdapterItem)?.Id;
        var adapters = await Task.Run(NetworkService.ListAdapters);
        adapterComboBox.DataSource = adapters.ToList();
        adapterComboBox.SelectedIndex = selected == null ? -1 : adapters.ToList().FindIndex(adapter => adapter.Id == selected);
        if (adapters.Count == 0) networkReportTextBox.Text = "No usable active IP adapter was found. Connect the network and refresh. Loopback and low-level WFP filter interfaces are excluded.";
    }
    private AdapterItem SelectedAdapter() => adapterComboBox.SelectedItem as AdapterItem
        ?? throw new InvalidOperationException("Select an active network adapter first.");
    private async Task RefreshAppsAsync()
    {
        await RunBusyAsync("Inventory installed applications", async _ =>
        {
            var apps = await Task.Run(() => AppPackageService.List(WriteLog));
            appInventory.Clear();
            foreach (OptionalApp app in apps)
            {
                var row = new OptionalAppRow(app); row.PropertyChanged += (_,_) =>
                    appsCount.Text = $"{optionalApps.Count} shown / {appInventory.Count} installed entries • {appInventory.Count(item => item.Selected)} selected";
                appInventory.Add(row);
            }
            RefreshAppFilter();
            return $"{apps.Count} installed entries found; {apps.Count(app => app.Protected)} protected. Review type/scope. Nothing is selected automatically.";
        });
    }
    private void RefreshHistory()
    {
        if (!runtimeReady) return;
        string? selectedId = (historyGrid.CurrentRow?.DataBoundItem as HistoryRow)?.Run.Id;
        JournalInspection inspection = store.Inspect();
        history.ReplaceWith(inspection.Runs.Select(run => new HistoryRow(run,machineId,userSid)));
        if (selectedId != null)
        {
            int index = history.ToList().FindIndex(row => row.Run.Id == selectedId);
            if (index >= 0) historyGrid.CurrentCell = historyGrid.Rows[index].Cells[0];
        }
        string signature = string.Join("\n",inspection.Problems);
        if (signature.Length > 0 && signature != historyProblemSignature) WriteActivity("Unreadable backups; new changes remain blocked until resolved:\n"+signature,ActivityLevel.Error);
        historyProblemSignature = signature;
        historyDescriptionLabel.Text = "Restore the newest setting batch first for this PC/account. Files/apps/maintenance cannot be undone. "
            + (inspection.Problems.Count > 0 ? $"{inspection.Problems.Count} unreadable backup(s); inspect Activity and the backup folder." : "Select a batch to inspect its entries and scope.");
        UpdateHistoryDetails();
    }
    private void UpdateHistoryDetails()
    {
        if (!runtimeReady) return;
        if (historyGrid.CurrentRow?.DataBoundItem is not HistoryRow selected) { historyDetailsTextBox.Text = "Select a history batch to read its per-action results."; return; }
        ChangeRun run = selected.Run;
        historyDetailsTextBox.Text = $"{run.StartedUtc.ToLocalTime():g}  |  {run.Title}\n\n"
            + string.Join("\n\n", run.Entries.Select(e => $"[{e.Status}] {e.Title}\n{e.Message}"));
    }

    private async Task RestoreLatestAsync()
    {
        if (!EnsureAdministrator()) return;
        ChangeRun? latest = store.List().FirstOrDefault(run => run.MachineId == machineId && run.UserSid == userSid && run.HasPendingUndo);
        if (latest == null) throw new InvalidOperationException("No pending setting backup is available for this PC/account.");
        if (DarkDialog.Show(this, $"Restore original settings from '{latest.Title}' ({latest.StartedUtc.ToLocalTime():g})? Current values for these settings will be replaced. File/app deletion and Windows repairs will not be undone.",
                "Restore settings", MessageBoxButtons.OKCancel, MessageBoxIcon.Information, MessageBoxDefaultButton.Button2) != DialogResult.OK) return;
        await RunBusyAsync("Restore settings", async token =>
        {
            var reporter = new Progress<RunProgress>(ReportRunProgress);
            ChangeRun result = await Task.Run(() => engine.RestoreAsync(latest.Id, reporter, token));
            operationOutcome = result.HasPendingUndo ? ActivityLevel.Warning : ActivityLevel.Success;
            RefreshHistory(); await RefreshOverviewAsync();
            return result.HasPendingUndo ? "Some settings still need restoration. Review History and retry."
                : "Original settings restored. Sign out/restart to fully apply.";
        });
    }

    private void WriteLog(string text) => WriteActivity(text, ActivityLevel.Info);

    private void WriteActivity(string text, ActivityLevel level)
    {
        if (resourcesClosed) return;
        DateTimeOffset now = DateTimeOffset.Now;
        int limit = Math.Max(4096, options.MaxLogCharacters);
        string visible = text.Length > limit ? text[..limit] + "\n[Further details are available in the exported log.]" : text;
        logQueue.Enqueue(new ActivityEntry(Interlocked.Increment(ref activitySequence), now, level, visible));
        try { logFile.WriteLine($"[{now:HH:mm:ss}] [{level}] {text}"); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ObjectDisposedException)
        {
            if (Interlocked.Exchange(ref logFailureReported, 1) == 0)
                logQueue.Enqueue(new ActivityEntry(Interlocked.Increment(ref activitySequence), now, ActivityLevel.Error,
                    "Log file could not be written: " + ex.Message));
        }
    }
    private void FlushLog()
    {
        if (resourcesClosed) return;
        bool changed = false;
        while (logQueue.TryDequeue(out ActivityEntry? entry))
        {
            activityEntries.Add(entry); activityCharacters += entry.Details.Length; changed = true;
        }
        while (activityEntries.Count > 1 && (activityEntries.Count > UiPalette.MaximumActivityEntries || activityCharacters > options.MaxLogCharacters))
        {
            activityCharacters -= activityEntries[0].Details.Length; activityEntries.RemoveAt(0);
        }
        if (changed) RefreshActivityView();
    }
    private void RefreshActivityView()
    {
        long selectedId = (activityGrid.CurrentRow?.DataBoundItem as ActivityEntry)?.Id ?? -1;
        int firstRow = Math.Max(0, activityGrid.FirstDisplayedScrollingRowIndex);
        string search = activitySearchBox.Text.Trim();
        ActivityLevel? filter = activityFilterComboBox.SelectedIndex switch
        {
            1 => ActivityLevel.Success, 2 => ActivityLevel.Warning, 3 => ActivityLevel.Error, _ => null
        };
        ActivityEntry[] filtered = activityEntries.Where(e => (filter == null || e.Level == filter)
            && (search.Length == 0 || e.Details.Contains(search, StringComparison.OrdinalIgnoreCase)))
            .TakeLast(UiPalette.MaximumActivityRows).ToArray();
        updatingActivity = true;
        try
        {
            activityRows.ReplaceWith(filtered);
            if (filtered.Length > 0)
            {
                int row = followActivityCheckBox.Checked ? filtered.Length - 1 : Array.FindIndex(filtered, e => e.Id == selectedId);
                row = Math.Max(0, row);
                activityGrid.CurrentCell = activityGrid.Rows[row].Cells[0];
                if (followActivityCheckBox.Checked) activityGrid.FirstDisplayedScrollingRowIndex = row;
                else activityGrid.FirstDisplayedScrollingRowIndex = Math.Min(firstRow, filtered.Length - 1);
            }
        }
        finally { updatingActivity = false; }
        activityCountLabel.Text = $"{filtered.Length} shown / {activityEntries.Count} recent events  •  "
            + $"{activityEntries.Count(e => e.Level == ActivityLevel.Warning)} warnings  •  {activityEntries.Count(e => e.Level == ActivityLevel.Error)} errors";
        UpdateActivityDetails();
    }
    private void UpdateActivityDetails()
    {
        if (updatingActivity) return;
        activityDetailsTextBox.Text = activityGrid.CurrentRow?.DataBoundItem is ActivityEntry entry
            ? $"{entry.At.ToLocalTime():yyyy-MM-dd HH:mm:ss}  |  {entry.Status}\n\n{entry.Details}"
            : "Select an event to read its complete details.";
    }
    private void InitializeDesktopNotifications()
    {
        desktopMenu = new ContextMenuStrip { Renderer = new DarkMenuRenderer(), BackColor = UiPalette.Surface, ForeColor = UiPalette.Text, ShowImageMargin = true };
        UiIcons.AddMenuItem(desktopMenu.Items, "Open BoosterX", UiIcon.Dashboard, (_, _) => RestoreFromTray());
        UiIcons.AddMenuItem(desktopMenu.Items, "Open Activity", UiIcon.Activity, (_, _) => { RestoreFromTray(); ShowPage(activityPage, activityNavButton); });
        desktopMenu.Items.Add(new ToolStripSeparator());
        UiIcons.AddMenuItem(desktopMenu.Items, "Exit", UiIcon.Error, (_, _) => Close());
        desktopIcon = new NotifyIcon { Icon = ownedIcon, Text = "BoosterX", ContextMenuStrip = desktopMenu,
            Visible = uiPreferences.DesktopNotifications };
        desktopIcon.DoubleClick += (_, _) => RestoreFromTray();
        desktopIcon.BalloonTipClicked += (_, _) => { RestoreFromTray(); ShowPage(activityPage, activityNavButton); };
    }
    private void RestoreFromTray()
    {
        Show();
        if (WindowState == FormWindowState.Minimized) WindowState = FormWindowState.Normal;
        Activate();
    }
    private void NotifyDesktop(string title, string text, ToolTipIcon icon, bool force = false)
    {
        if (desktopIcon == null || !uiPreferences.DesktopNotifications) return;
        if (!force && !uiPreferences.NotifyInForeground && WindowState != FormWindowState.Minimized && ContainsFocus) return;
        desktopIcon.Visible = true;
        try
        {
            desktopIcon.ShowBalloonTip(5000, "BoosterX — " + title,
                text.Length > 240 ? text[..237] + "…" : text, icon);
        }
        catch (System.Runtime.InteropServices.ExternalException ex)
        { WriteActivity("Windows could not display the notification: " + ex.Message, ActivityLevel.Warning); }
    }
    private async Task GuardReadOnlyAsync(Func<Task> action)
    {
        if (!runtimeReady) return;
        try { await action(); }
        catch (Exception ex)
        {
            WriteActivity(ex.ToString(), ActivityLevel.Error);
            DarkDialog.Show(this, ex.Message, "BoosterX", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
    private void ActivityFilterChanged(object? sender, EventArgs e) { if (activityGrid != null) RefreshActivityView(); }
    private void ActivityGrid_SelectionChanged(object? sender, EventArgs e) => UpdateActivityDetails();
    private void ActivityGrid_CellFormatting(object? sender, DataGridViewCellFormattingEventArgs e)
    {
        if (e.RowIndex < 0 || e.ColumnIndex != activityStatusColumn.Index || activityGrid.Rows[e.RowIndex].DataBoundItem is not ActivityEntry entry) return;
        e.CellStyle.ForeColor = entry.Level switch
        {
            ActivityLevel.Success => UiPalette.Success, ActivityLevel.Warning => UiPalette.Warning,
            ActivityLevel.Error => UiPalette.Error, _ => UiPalette.Muted
        };
    }
    private async void UiPreferencesChanged(object? sender, EventArgs e) => await GuardReadOnlyAsync(() =>
    {
        uiPreferences.FollowActivity = followActivityCheckBox.Checked;
        uiPreferences.DesktopNotifications = notificationsCheckBox.Checked;
        uiPreferences.NotifyInForeground = foregroundNotificationsCheckBox.Checked;
        if (desktopIcon != null) desktopIcon.Visible = uiPreferences.DesktopNotifications;
        uiPreferences.Save(dataDirectory); RefreshActivityView();
        return Task.CompletedTask;
    });
    private async void CopyActivityButton_Click(object? sender, EventArgs e) => await GuardReadOnlyAsync(() =>
    {
        if (activityGrid.CurrentRow?.DataBoundItem is not ActivityEntry entry) throw new InvalidOperationException("Select an event first.");
        Clipboard.SetText($"{entry.At.ToLocalTime():yyyy-MM-dd HH:mm:ss} [{entry.Status}] {entry.Details}");
        return Task.CompletedTask;
    });
    private void ClearActivityButton_Click(object? sender, EventArgs e)
    {
        FlushLog(); activityEntries.Clear(); activityCharacters = 0; RefreshActivityView();
        SetStatus("Activity view cleared. The complete session log is still available for export.");
    }
    private async void TestNotificationButton_Click(object? sender, EventArgs e) => await GuardReadOnlyAsync(() =>
    {
        if (!uiPreferences.DesktopNotifications) throw new InvalidOperationException("Enable Desktop notifications first.");
        NotifyDesktop("Notification test", "BoosterX is ready. Operation results will appear here when the window is in the background.", ToolTipIcon.Info, force: true);
        SetStatus("Notification requested. Windows notification settings determine whether it is displayed.");
        return Task.CompletedTask;
    });
    private void SetStatus(string message)
    {
        statusLabel.Text = message.Length <= 180 ? message : message[..177] + "…";
        help.SetToolTip(statusLabel, message);
    }
    private void SaveText(string text, string filename)
    {
        using var dialog = new SaveFileDialog { FileName = filename, Filter = "Text files (*.txt)|*.txt", DefaultExt = "txt", AddExtension = true };
        if (dialog.ShowDialog(this) == DialogResult.OK) File.WriteAllText(dialog.FileName, text, Encoding.UTF8);
    }
    private static void OpenFile(string path) => Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
    private static void OpenUri(string uri) => Process.Start(new ProcessStartInfo(uri) { UseShellExecute = true });

    private static void SelectionGrid_CurrentCellDirtyStateChanged(object? sender, EventArgs e)
    {
        if (sender is DataGridView { IsCurrentCellDirty: true, CurrentCell: DataGridViewCheckBoxCell } grid)
            grid.CommitEdit(DataGridViewDataErrorContexts.Commit);
    }
    private void SelectionGrid_CellBeginEdit(object? sender, DataGridViewCellCancelEventArgs e)
    {
        if (busy || sender is DataGridView grid && grid.Rows[e.RowIndex].DataBoundItem is ISelectableRow { CanSelect: false }) e.Cancel = true;
    }
    private void SelectionGrid_DataError(object? sender, DataGridViewDataErrorEventArgs e)
    {
        e.ThrowException = false;
        WriteLog("Grid edit failed: " + (e.Exception?.Message ?? "Unknown edit error."));
        SetStatus("Selection could not be updated. Refresh the list before applying changes.");
    }
    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (busy)
        {
            e.Cancel = true;
            DarkDialog.Show(this, "Wait for the current action to finish. Stop after current action skips remaining changes without killing Windows servicing.", "BoosterX");
        }
        base.OnFormClosing(e);
    }
    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        DisposeApplicationResources();
        base.OnFormClosed(e);
    }
    private void DisposeApplicationResources()
    {
        if (resourcesClosed) return;
        activityTimer?.Stop(); FlushLog(); resourcesClosed = true;
        if (desktopIcon != null) { desktopIcon.Visible = false; desktopIcon.Dispose(); }
        desktopMenu?.Dispose();
        logFile.Dispose(); cancellation?.Dispose(); help.Dispose();
        if (brandIconBox != null) brandIconBox.Image = null;
        Icon = null;
        brandImage?.Dispose(); ownedIcon?.Dispose();
    }

    private void OverviewNavButton_Click(object? sender, EventArgs e) => ShowPage(overviewPage, overviewNavButton);
    private void OptimizeNavButton_Click(object? sender, EventArgs e) => ShowPage(optimizePage, optimizeNavButton);
    private void CleanupNavButton_Click(object? sender, EventArgs e) => ShowPage(cleanupPage, cleanupNavButton);
    private void AppsNavButton_Click(object? sender, EventArgs e) => ShowPage(appsPage, appsNavButton);
    private void StartupNavButton_Click(object? sender, EventArgs e) => ShowPage(startupPage, startupNavButton);
    private void NetworkNavButton_Click(object? sender, EventArgs e) => ShowPage(networkPage, networkNavButton);
    private void RepairsNavButton_Click(object? sender, EventArgs e) => ShowPage(repairsPage, repairsNavButton);
    private void ActivityNavButton_Click(object? sender, EventArgs e) => ShowPage(activityPage, activityNavButton);
    private void HistoryNavButton_Click(object? sender, EventArgs e) => ShowPage(historyPage, historyNavButton);
    private void ActivityTimer_Tick(object? sender, EventArgs e) => FlushLog();
    private void HistoryGrid_SelectionChanged(object? sender, EventArgs e) => UpdateHistoryDetails();
    private void StopButton_Click(object? sender, EventArgs e)
    {
        cancellation?.Cancel(); stopButton.Enabled = false;
        WriteLog("Stop requested. The current change or servicing command is allowed to finish.");
        SetStatus("Stopping after the current action finishes…");
    }
    private async void ElevateButton_Click(object? sender, EventArgs e) => await GuardAsync(() => { RestartElevated(); return Task.CompletedTask; });
    private async void SaveLogButton_Click(object? sender, EventArgs e) => await GuardReadOnlyAsync(() =>
    {
        FlushLog(); logFile.Flush();
        using var dialog = new SaveFileDialog { FileName = "BoosterX-activity.log", Filter = "Log files (*.log)|*.log|All files (*.*)|*.*", AddExtension = true, DefaultExt = "log" };
        if (dialog.ShowDialog(this) == DialogResult.OK && !Path.GetFullPath(dialog.FileName).Equals(Path.GetFullPath(logPath), StringComparison.OrdinalIgnoreCase))
            File.Copy(logPath, dialog.FileName, overwrite: true);
        return Task.CompletedTask;
    });
    private async void AnalyzeButton_Click(object? sender, EventArgs e) => await GuardAsync(async () =>
    {
        await RunBusyAsync("Analyze this PC", async token =>
        {
            string report = await Task.Run(() => DiagnosticsService.AnalyzeAsync(token), token);
            reportTextBox.Text = report; WriteLog(report); await RefreshOverviewAsync();
            return "Analysis complete. No settings changed.";
        });
    });
    private void ReviewPlanButton_Click(object? sender, EventArgs e) => ShowPage(optimizePage, optimizeNavButton);
    private async void SaveReportButton_Click(object? sender, EventArgs e) => await GuardAsync(() => { SaveText(reportTextBox.Text, "BoosterX-diagnostics.txt"); return Task.CompletedTask; });
    private async void SettingsButton_Click(object? sender, EventArgs e) => await GuardAsync(() => { OpenFile(Path.Combine(dataDirectory, "settings.json")); return Task.CompletedTask; });
    private async void ApplyButton_Click(object? sender, EventArgs e) => await GuardAsync(async () =>
    {
        optimizationGrid.EndEdit();
        await ApplyPlanAsync("Windows preferences", preferenceInventory.Where(row => row.Selected).Select(row => operations.Optimization(row.Item)).ToArray());
    });
    private void RecommendedButton_Click(object? sender, EventArgs e) { foreach (var row in preferenceInventory) row.Selected = row.Item.Recommended; }
    private void ClearOptimizationsButton_Click(object? sender, EventArgs e) { foreach (var row in preferenceInventory) row.Selected = false; }
    private async void ScanTempButton_Click(object? sender, EventArgs e) => await GuardAsync(async () =>
    {
        temporaryFiles.ReplaceWith([]);
        cleanupInfoLabel.Text = "Scanning cleanup areas. Active or unavailable caches will be skipped.";
        await RunBusyAsync("Scan old temporary files", async token =>
        {
            var scanProgress = new Progress<ScanProgress>(p => SetStatus($"Scanning: {p.Visited:N0} entries visited / {p.Eligible:N0} eligible files…"));
            TempScan scan = await Task.Run(() => operations.Cleanup.Scan(token,scanProgress,WriteLog), token);
            temporaryFiles.ReplaceWith(scan.Files.Select(file => new TempRow(file)));
            cleanupInfoLabel.Text = $"{scan.Files.Count:N0} eligible files / {CleanupService.FormatBytes(scan.Bytes)} logical bytes / {scan.Skipped:N0} skipped"
                + (scan.Truncated ? " / scan limit reached." : ".");
            WriteLog(cleanupInfoLabel.Text); return "Scan complete. Review the list before deleting.";
        });
    });
    private async void DeleteTempButton_Click(object? sender, EventArgs e) => await GuardAsync(async () =>
    {
        cleanupGrid.EndEdit();
        TempCandidate[] selected = temporaryFiles.Where(row => row.Selected).Select(row => row.Item).ToArray();
        if (selected.Length == 0) throw new InvalidOperationException("Scan and select temporary files first.");
        bool executed = await ApplyPlanAsync("Temporary cleanup", [operations.CleanTemp(selected)],
            $"Permanently delete up to {selected.Length:N0} files / {CleanupService.FormatBytes(selected.Sum(file => file.Length))}? Deleted files cannot be restored by BoosterX. Changed/locked files are skipped.");
        if (executed) { temporaryFiles.ReplaceWith([]); cleanupInfoLabel.Text = "Scan again to refresh the list."; }
    });
    private async void RecycleButton_Click(object? sender, EventArgs e) => await GuardAsync(async () =>
    {
        var size = CleanupService.GetRecycleBin();
        await ApplyPlanAsync("Empty Recycle Bin", [WindowsOperations.EmptyRecycleBin()],
            $"Permanently empty this account's Recycle Bin on local drives? Windows reports {size.Items:N0} items / {CleanupService.FormatBytes(size.Bytes)}. BoosterX cannot restore these files.");
    });
    private async void StorageButton_Click(object? sender, EventArgs e) => await GuardAsync(() => { OpenUri("ms-settings:storagesense"); return Task.CompletedTask; });
    private async void FindAppsButton_Click(object? sender, EventArgs e) => await GuardAsync(RefreshAppsAsync);
    private async void RemoveAppsButton_Click(object? sender, EventArgs e) => await GuardAsync(async () =>
    {
        appsGrid.EndEdit();
        var plan = appInventory.Where(row => row.Selected).Select(row => WindowsOperations.RemoveApp(row.Item,ReportNativePercent)).ToArray();
        if (await ApplyPlanAsync("Application removal", plan,
                "Uninstall selected applications? Review each item's scope: machine-wide desktop apps affect all users. Publisher uninstallers may prompt or keep shared files/profiles. Package removal can delete app data. BoosterX cannot undo app/file removal; folders and registry entries are not force-deleted."))
            await RefreshAppsAsync();
    });
    private async void StoreAppButton_Click(object? sender, EventArgs e) => await GuardAsync(() =>
    {
        if (appsGrid.CurrentRow?.DataBoundItem is not OptionalAppRow row) throw new InvalidOperationException("Highlight an app row first.");
        OpenUri(row.Item.Kind == InstalledAppKind.Appx ? "ms-windows-store://search/?query=" + Uri.EscapeDataString(row.Item.Name) : "ms-settings:appsfeatures"); return Task.CompletedTask;
    });
    private async void InstalledAppsButton_Click(object? sender, EventArgs e) => await GuardAsync(() => { OpenUri("ms-settings:appsfeatures"); return Task.CompletedTask; });
    private async void RefreshStartupButton_Click(object? sender, EventArgs e) => await GuardAsync(async () =>
    {
        await RunBusyAsync("Read startup entries", async _ =>
        {
            var entries = await Task.Run(StartupService.List);
            startupItems.ReplaceWith(entries.Select(entry => new StartupRow(entry)));
            foreach (DataGridViewRow row in startupGrid.Rows)
                if (row.DataBoundItem is StartupRow { CanSelect: false }) row.DefaultCellStyle.ForeColor = SystemInformation.HighContrast ? SystemColors.GrayText : Theme.Muted;
            return $"{entries.Count} Run entries found. Protected security entries cannot be selected.";
        });
    });
    private async void DisableStartupButton_Click(object? sender, EventArgs e) => await GuardAsync(async () =>
    {
        startupGrid.EndEdit();
        if (await ApplyPlanAsync("Disable startup entries", startupItems.Where(row => row.Selected).Select(row => operations.DisableStartup(row.Item)).ToArray())) startupItems.ReplaceWith([]);
    });
    private async void StartupSettingsButton_Click(object? sender, EventArgs e) => await GuardAsync(() => { OpenUri("ms-settings:startupapps"); return Task.CompletedTask; });
    private async void DiagnoseNetworkButton_Click(object? sender, EventArgs e) => await GuardAsync(async () =>
    {
        AdapterItem adapter = SelectedAdapter();
        await RunBusyAsync("Network diagnostics", async token =>
        {
            string report = await Task.Run(() => operations.Network.DiagnoseAsync(adapter.Id, token), token);
            networkReportTextBox.Text = report; WriteLog(report); return "Network diagnostics complete. No settings changed.";
        });
    });
    private async void ApplyDnsButton_Click(object? sender, EventArgs e) => await GuardAsync(async () =>
    {
        AdapterItem adapter = SelectedAdapter();
        if (dnsComboBox.SelectedItem is not DnsPreset preset) throw new InvalidOperationException("Choose an IPv4 DNS provider first.");
        await ApplyPlanAsync("IPv4 DNS configuration", [operations.SetDns(adapter, preset)]);
    });
    private async void RefreshAdaptersButton_Click(object? sender, EventArgs e) => await GuardAsync(async () =>
    { await RunBusyAsync("Read network adapters", async _ => { await RefreshAdaptersAsync(); return "Adapter list refreshed."; }); });
    private async void FlushDnsButton_Click(object? sender, EventArgs e) => await GuardAsync(async () => { await ApplyPlanAsync("DNS cache repair", [operations.WindowsRepair("flush-dns")]); });
    private async Task RepairAsync(string title, string[] ids, string? confirmation = null)
        => await ApplyPlanAsync(title, ids.Select(operations.WindowsRepair).ToArray(), confirmation);
    private async void DismScanButton_Click(object? sender, EventArgs e) => await GuardAsync(() => RunRepairDefinitionAsync("dism-scan"));
    private async void RepairWindowsButton_Click(object? sender, EventArgs e) => await GuardAsync(() => RepairAsync("Repair Windows image + system files", ["dism-repair", "sfc"],
        "Run DISM RestoreHealth followed by SFC scannow? Windows can download repair content and take time. Review command output in Activity; no automatic reboot is performed and there is no BoosterX undo."));
    private async void DiskScanButton_Click(object? sender, EventArgs e) => await GuardAsync(() => RunRepairDefinitionAsync("disk-scan"));
    private async void ComponentsButton_Click(object? sender, EventArgs e) => await GuardAsync(() => RunRepairDefinitionAsync("components"));
    private async void WinsockButton_Click(object? sender, EventArgs e) => await GuardAsync(() => RepairAsync("Reset Winsock", ["winsock"],
        "Reset the Winsock catalog? VPN/security network providers may need reinstallation and a restart is required. BoosterX cannot undo this reset."));
    private async void LegacyServicesButton_Click(object? sender, EventArgs e) => await GuardAsync(async () =>
    {
        await ApplyPlanAsync("Legacy service recovery", ServiceManager.LegacyRepairDefaults.Keys.Select(operations.RepairLegacyService).ToArray(),
            "Enable only disabled wuauserv, UsoSvc, SysMain, WSearch and DiagTrack using BoosterX recovery presets? This includes services deliberately disabled elsewhere. Non-disabled configurations are preserved; original states are backed up.");
    });
    private async void RestorePointButton_Click(object? sender, EventArgs e) => await GuardAsync(async () =>
    {
        if (!EnsureAdministrator()) return;
        await RunBusyAsync("Request Windows restore point", async _ =>
        { string message = await Task.Run(RestorePointService.TryCreate); WriteLog(message); return message; });
    });
    private async void SystemProtectionButton_Click(object? sender, EventArgs e) => await GuardAsync(() =>
    { OpenFile(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "SystemPropertiesProtection.exe")); return Task.CompletedTask; });
    private async void NetworkSettingsButton_Click(object? sender, EventArgs e) => await GuardAsync(() => { OpenUri("ms-settings:network-status"); return Task.CompletedTask; });
    private async void RestoreLatestButton_Click(object? sender, EventArgs e) => await GuardAsync(RestoreLatestAsync);
    private async void HistoryDetailsButton_Click(object? sender, EventArgs e) => await GuardAsync(() =>
    {
        if (historyGrid.CurrentRow?.DataBoundItem is not HistoryRow row) throw new InvalidOperationException("Highlight a history batch first.");
        ChangeRun run = store.Load(row.Run.Id);
        historyDetailsTextBox.Text = $"{run.StartedUtc.ToLocalTime():g}  |  {run.Title}\n\n"
            + string.Join("\n\n", run.Entries.Select(entry => $"[{entry.Status}] {entry.Title}\n{entry.Message}"));
        return Task.CompletedTask;
    });
    private async void RefreshHistoryButton_Click(object? sender, EventArgs e) => await GuardAsync(() => { RefreshHistory(); return Task.CompletedTask; });
    private async void OpenBackupsButton_Click(object? sender, EventArgs e) => await GuardAsync(() =>
    { Directory.CreateDirectory(store.DirectoryPath); OpenFile(store.DirectoryPath); return Task.CompletedTask; });
}
