using System.Text.Json;
using BoosterX.Core;
using BoosterX.App.Controls;
using System.Drawing;

// These checks use isolated temporary files and a fake settings backend. They never tune the host PC.
int passed = 0;
var checks = new (string Name, Func<Task> Run)[]
{
    ("Original value is durable before mutation", DurableBeforeApply),
    ("Failed capture prevents modification", CaptureFailure),
    ("Failed journal write prevents modification", JournalFailure),
    ("Partial change rolls back after failure", Rollback),
    ("Failed rollback blocks further modifications", FailedRollback),
    ("Interrupted applying record is recoverable", CrashRecovery),
    ("Restore runs in reverse action order", ReverseRestore),
    ("Batches restore newest first", LatestFirst),
    ("Clock changes cannot reorder setting restoration", ClockChange),
    ("Foreign machine and account cannot restore", IdentityGuard),
    ("Stop finishes the current action and skips the rest", StopBetweenSteps),
    ("Irreversible operations are not offered as restorable", NoFakeUndo),
    ("Failed restore preserves order and can be retried", RetryRestore),
    ("Duplicate action IDs are rejected", DuplicateActions),
    ("Corrupt journals are reported", CorruptJournal),
    ("Journal identifiers cannot traverse paths", JournalPathGuard),
    ("Clean scan respects ages, read-only files and root containment", ScanEligibility),
    ("Clean scan skips directory and file links", SkipLinks),
    ("Clean scan bounds total entries", ScanBound),
    ("Cancelled scan stops before returning candidates", CancelScan),
    ("Invalid configuration is rejected", ConfigurationValidation),
    ("Configuration values round-trip", ConfigurationRoundTrip),
    ("Color contrast calculation matches black/white reference", ContrastReference),
    ("Text remains readable on enabled, selected and disabled surfaces", PaletteContrast),
    ("Scrollbar endpoints and content fit never divide by zero", ScrollEndpoints),
    ("Scrollbar mapping is monotonic and clamps dragged positions", ScrollMapping),
    ("Scrollbar math handles maximum-size extents without overflow", ScrollLargeExtent),
    ("Window hit testing recognizes all corners and outside points", ResizeHitTesting),
    ("Dragging left and top keeps the opposite corner fixed", ResizeOppositeCorner),
    ("Every resize direction enforces the minimum window size", ResizeMinimum),
    ("Unequal percent rows each retain their minimum usable height", PercentRowMinimums),
    ("Percent row allocation is independent of weight normalization", PercentWeightScale),
    ("Percent layout rounds upward so the smallest row never clips", PercentRounding),
    ("Percent extent handles empty, invalid and huge constraints", PercentBounds),
    ("Default launch is centered at 1280x720 on a roomy screen", DefaultWindowBounds),
    ("Small screens and taskbars constrain startup bounds", SmallScreenBounds),
    ("Resolution and DPI matrix stays inside the work area", ResponsiveScreenMatrix),
    ("Negative-coordinate monitors preserve their own bounds", NegativeMonitorBounds),
    ("Monitor changes clamp without resetting valid user sizes", MonitorChangeBounds),
    ("Sidebar widens normally and yields to the compact menu", SidebarAdaptation),
    ("Layout breakpoints preserve logical behavior across DPI", DpiLayoutModes),
    ("Focus reveal brings small actions into the viewport", RevealSmallActions),
    ("Focus reveal preserves useful positions inside large viewers", RevealLargeViewers),
    ("Header reserves both full action buttons on a roomy screen", HeaderNaturalSizes),
    ("Header stacks the complete toolbar before it can clip", HeaderMeasuredOverflow),
    ("Oversized toolbars yield to the command menu", HeaderMenuFallback),
    ("Header sizing survives resize cycles across DPI", HeaderDpiCycles),
    ("Icon and text form one aligned group with a real gap", IconTextAlignment),
    ("Icon and text remain bounded in tiny controls", IconTinyBounds),
    ("Icon preferred sizes reserve the icon at every DPI", IconPreferredSizes),
    ("Every action icon has valid font-independent vector geometry", IconVectorDefinitions),
    ("Fine wheel deltas accumulate without disappearing", FineWheelInput),
    ("Wheel input respects disabled, line and page settings", WheelSettings),
    ("Nested scrolling yields only at the requested edge", WheelEdgeHandoff),
    ("Changing wheel axes cannot reuse a partial vertical notch", WheelAxisChange),
    ("Extreme wheel values clamp without arithmetic overflow", WheelLargeValues),
    ("Dropdown placement fits above or below within each monitor", PopupWorkingAreas),
    ("Dropdown hit testing includes its scrolled offset", PopupRowHitTesting),
    ("Variable grid row heights retain access to the final row", GridVariableRows),
    ("Grid scrolling maps only eligible rows and clamps extremes", GridRowBounds),
    ("Expanded preferences retain original IDs and opt-in defaults", ExpandedPreferenceDefaults),
    ("Expanded registry targets are unique and typed", PreferenceTargetIntegrity),
    ("Configured menu delay is resolved without changing other values", PreferenceConfiguredValues),
    ("Repair inventory uses fixed supported command arguments", RepairInventoryIntegrity),
    ("System drive substitution rejects injected command arguments", RepairDriveArguments),
    ("Quoted and unquoted publisher commands preserve their arguments", UninstallParsing),
    ("Publisher commands reject shell hosts and remote/relative executables", UninstallHostProtection),
    ("Application inventory protects shared and system components", InstalledAppProtection),
    ("Progress combines completed actions and command fractions", ProgressStepFractions),
    ("Unknown or completed progress never invents a timed estimate", ProgressUnknownAndComplete),
    ("Command percentages parse invariantly and reject invalid numbers", NativePercentParsing),
    ("Cache scanning honors the selected file allowlist", FilteredCacheScan),
    ("Cache scanning reports real visited and eligible counts", ScanProgressCounts),
    ("History inspection keeps valid runs and reports corrupt files", InspectHistory),
    ("History inspection does not bypass the mutation guard", InspectStillBlocksMutation),
    ("Incomplete journal entries are rejected before restoration", IncompleteHistory),
    ("Idle cleanup queries both Windows Update and BITS without the mutation allowlist", CleanupIdleServices),
    ("Running, paused and pending update services exclude their cache", CleanupActiveServices),
    ("Unavailable service state skips update cache instead of failing cleanup", CleanupUnavailableServices),
    ("Pending restart excludes update cache and unrelated areas do not query services", CleanupPendingRestart),
    ("Unknown cleanup service names are rejected before native queries", CleanupServiceNames),
    ("Maintenance rows keep full actions and descriptions separate across DPI", MaintenanceRowBounds),
    ("Narrow maintenance rows stack details without losing text height", MaintenanceStackedRows),
    ("Maintenance fine scrolling advances less than one action row", MaintenanceWheelStep)
};
foreach (var check in checks)
{
    try { await check.Run(); Console.WriteLine("PASS " + check.Name); passed++; }
    catch (Exception ex) { Console.Error.WriteLine("FAIL " + check.Name + "\n" + ex); return 1; }
}
Console.WriteLine($"{passed}/{checks.Length} checks passed.");
return 0;

static void Assert(bool value, string message = "Assertion failed") { if (!value) throw new Exception(message); }
static async Task Throws<T>(Func<Task> action) where T : Exception
{
    try { await action(); } catch (T) { return; }
    throw new Exception("Expected " + typeof(T).Name);
}
static Task Sync(Action action) { action(); return Task.CompletedTask; }

static Task ContrastReference() => Sync(() =>
{
    Assert(Math.Abs(UiPalette.Contrast(Color.Black, Color.White) - 21) < 0.001);
    Assert(Math.Abs(UiPalette.Contrast(UiPalette.Text, UiPalette.Text) - 1) < 0.001);
});
static Task PaletteContrast() => Sync(() =>
{
    foreach (Color surface in new[] { UiPalette.Background, UiPalette.Surface, UiPalette.Card, UiPalette.ButtonSurface, UiPalette.Hover,
                 UiPalette.Selection, UiPalette.Accent, UiPalette.AccentHover, UiPalette.AccentPressed })
        Assert(UiPalette.Contrast(UiPalette.Text, surface) >= 4.5, "Text contrast is below 4.5:1 on " + surface);
    foreach (Color surface in new[] { UiPalette.Background, UiPalette.Surface, UiPalette.Card, UiPalette.DisabledSurface })
    {
        Assert(UiPalette.Contrast(UiPalette.DisabledText, surface) >= 4.5, "Disabled text is unreadable.");
        Assert(UiPalette.Contrast(UiPalette.Muted, surface) >= 4.5, "Secondary text is unreadable.");
    }
    foreach (Color foreground in new[] { UiPalette.Success, UiPalette.Warning, UiPalette.Error })
    foreach (Color surface in new[] { UiPalette.Surface, UiPalette.Card })
        Assert(UiPalette.Contrast(foreground, surface) >= 4.5, "Activity status text is unreadable.");
});

static Task ScrollEndpoints() => Sync(() =>
{
    foreach (int track in new[] { 0, 1, 8, 100, 1000 })
    {
        ScrollGeometry fit = ScrollGeometry.Create(track, 100, 50, 28);
        Assert(fit.Range == 0 && fit.ThumbLength == track && fit.Travel == 0 && fit.Offset(42) == 0);
        ScrollGeometry full = ScrollGeometry.Create(track, 100, 1000, 28);
        Assert(full.ThumbLength >= 0 && full.ThumbLength <= track && full.Travel >= 0);
        Assert(full.Position(0) == 0 && full.Position(full.Range) == full.Travel);
        Assert(full.Offset(full.Travel) == (full.Travel > 0 ? full.Range : 0));
    }
});
static Task ScrollMapping() => Sync(() =>
{
    ScrollGeometry g = ScrollGeometry.Create(317, 37, 4321, 28);
    int previous = -1;
    for (int value = 0; value <= g.Range; value++)
    {
        int position = g.Position(value);
        Assert(position >= previous && position <= g.Travel);
        Assert(Math.Abs(g.Offset(position) - value) <= (g.Range + g.Travel - 1) / g.Travel);
        previous = position;
    }
    Assert(g.Position(-100) == 0 && g.Offset(-100) == 0);
    Assert(g.Position(int.MaxValue) == g.Travel && g.Offset(int.MaxValue) == g.Range);
});
static Task ScrollLargeExtent() => Sync(() =>
{
    ScrollGeometry g = ScrollGeometry.Create(2000, 700, int.MaxValue, 28);
    Assert(g.Range == int.MaxValue - 700 && g.ThumbLength == 28 && g.Travel == 1972);
    Assert(g.Position(g.Range) == g.Travel && g.Offset(g.Travel) == g.Range);
    ScrollGeometry zero = ScrollGeometry.Create(10, 0, 0, 28);
    Assert(zero.Range == 0 && zero.Travel == 0);
});
static Task ResizeHitTesting() => Sync(() =>
{
    Size size = new(940, 660);
    Assert(WindowGeometry.Hit(new Point(0, 0), size, 7) == (ResizeEdges.Left | ResizeEdges.Top));
    Assert(WindowGeometry.Hit(new Point(939, 0), size, 7) == (ResizeEdges.Right | ResizeEdges.Top));
    Assert(WindowGeometry.Hit(new Point(0, 659), size, 7) == (ResizeEdges.Left | ResizeEdges.Bottom));
    Assert(WindowGeometry.Hit(new Point(939, 659), size, 7) == (ResizeEdges.Right | ResizeEdges.Bottom));
    Assert(WindowGeometry.Hit(new Point(400, 300), size, 7) == ResizeEdges.None);
    Assert(WindowGeometry.Hit(new Point(-1, 0), size, 7) == ResizeEdges.None);
    Assert(WindowGeometry.Hit(new Point(940, 0), size, 7) == ResizeEdges.None);
});
static Task ResizeOppositeCorner() => Sync(() =>
{
    Rectangle original = new(-1400, 70, 1200, 820);
    foreach (Point delta in new[] { new Point(-600, -200), new Point(800, 700) })
    {
        Rectangle next = WindowGeometry.Resize(original, delta, ResizeEdges.Left | ResizeEdges.Top, new Size(900, 620));
        Assert(next.Right == original.Right && next.Bottom == original.Bottom);
        Assert(next.Width >= 900 && next.Height >= 620);
    }
});
static Task ResizeMinimum() => Sync(() =>
{
    Rectangle original = new(30, 40, 1200, 820);
    foreach (ResizeEdges edges in new[] { ResizeEdges.Left, ResizeEdges.Right, ResizeEdges.Top, ResizeEdges.Bottom,
        ResizeEdges.Left | ResizeEdges.Top, ResizeEdges.Right | ResizeEdges.Bottom })
    foreach (int amount in new[] { -10000, 10000 })
    {
        Rectangle next = WindowGeometry.Resize(original, new Point(amount, amount), edges, new Size(900, 620));
        Assert(next.Width >= 900 && next.Height >= 620);
        if (edges.HasFlag(ResizeEdges.Right)) Assert(next.Left == original.Left);
        if (edges.HasFlag(ResizeEdges.Bottom)) Assert(next.Top == original.Top);
    }
});

static Task PercentRowMinimums() => Sync(() =>
{
    PercentMinimum[] rows = [new(60, 120), new(40, 100)];
    int extent = ViewportGeometry.RequiredExtent(200, rows);
    Assert(extent == 450, "Summing minima clips the 40% detail row.");
    Assert((extent - 200) * 0.6 >= 120 && (extent - 200) * 0.4 >= 100);
});
static Task PercentWeightScale() => Sync(() =>
{
    int original = ViewportGeometry.RequiredExtent(85, [new(60, 300), new(40, 200)]);
    Assert(original == 585);
    Assert(original == ViewportGeometry.RequiredExtent(85, [new(6, 300), new(4, 200)]));
    Assert(original == ViewportGeometry.RequiredExtent(85, [new(0.6, 300), new(0.4, 200)]));
});
static Task PercentRounding() => Sync(() =>
{
    PercentMinimum[] rows = [new(33, 120), new(67, 100)];
    int extent = ViewportGeometry.RequiredExtent(80, rows);
    Assert(extent == 444);
    Assert((extent - 80) * 0.33 >= 120);
    Assert((extent - 81) * 0.33 < 120, "The minimal safe height should be used.");
});
static Task PercentBounds() => Sync(() =>
{
    Assert(ViewportGeometry.RequiredExtent(25, []) == 25);
    Assert(ViewportGeometry.RequiredExtent(-100, [new(0, 200), new(double.NaN, 200)]) == 0);
    Assert(ViewportGeometry.RequiredExtent(100, [new(1e-20, int.MaxValue), new(100, 10)]) == int.MaxValue);
    Assert(ViewportGeometry.RequiredExtent(10, [new(40, 0), new(60, 0)]) == 10);
});

static Task DefaultWindowBounds() => Sync(() =>
{
    Rectangle initial = ResponsiveGeometry.Initial(new Rectangle(0, 0, 1920, 1040), 96);
    Assert(initial == new Rectangle(320, 160, 1280, 720));
    Assert(ResponsiveGeometry.Minimum(new Rectangle(0, 0, 1920, 1040), 96) == new Size(640, 400));
});
static Task SmallScreenBounds() => Sync(() =>
{
    Rectangle work = new(0, 0, 1280, 680);
    Rectangle initial = ResponsiveGeometry.Initial(work, 96);
    Assert(initial.Width == 1248 && initial.Height == 648 && work.Contains(initial));
    Rectangle tiny = new(50, 60, 1, 1);
    Assert(ResponsiveGeometry.Initial(tiny, 384) == tiny);
    Assert(ResponsiveGeometry.Minimum(tiny, 384) == new Size(1, 1));
});
static Task ResponsiveScreenMatrix() => Sync(() =>
{
    Size[] screens = [new(480, 320), new(640, 480), new(800, 600), new(1024, 768), new(1280, 720),
        new(1366, 768), new(1600, 900), new(1920, 1080), new(2560, 1440), new(3440, 1440),
        new(3840, 2160), new(7680, 4320), new(1080, 1920)];
    foreach (Size screen in screens)
    foreach (int dpi in new[] { 96, 120, 144, 168, 192, 240, 288, 384 })
    foreach (int taskbar in new[] { 0, 40, 80 })
    {
        Rectangle work = new(0, 0, screen.Width, Math.Max(1, screen.Height - taskbar));
        Rectangle available = ResponsiveGeometry.Available(work, dpi);
        Rectangle initial = ResponsiveGeometry.Initial(work, dpi);
        Assert(available.Contains(initial), $"Offscreen startup: {screen}, {dpi} DPI.");
        Size minimum = ResponsiveGeometry.Minimum(work, dpi);
        Assert(initial.Width >= minimum.Width && initial.Height >= minimum.Height);
        ResponsiveMetrics layout = ResponsiveGeometry.Layout(initial.Size, dpi);
        Assert(layout.SidebarWidth + layout.SidebarGap + 2 * layout.PaddingHorizontal < initial.Width || layout.SidebarWidth == 0);
        Assert(ResponsiveGeometry.Fit(new Rectangle(-9999, -9999, 20000, 20000), work, dpi) == available);
    }
});
static Task NegativeMonitorBounds() => Sync(() =>
{
    Rectangle work = new(-2560, -200, 2560, 1400);
    Rectangle initial = ResponsiveGeometry.Initial(work, 144);
    Assert(initial.Size == new Size(1920, 1080) && work.Contains(initial));
    Assert(initial.X < 0);
    Assert(ResponsiveGeometry.Fit(new Rectangle(1500, 0, 1280, 720), work, 96).Right <= work.Right);
});
static Task MonitorChangeBounds() => Sync(() =>
{
    Rectangle work = new(0, 0, 1920, 1040), valid = new(100, 120, 1000, 600);
    Assert(ResponsiveGeometry.Fit(valid, work, 96) == valid);
    Rectangle moved = ResponsiveGeometry.Fit(new Rectangle(2200, 1000, 1900, 1200), new Rectangle(-800, 0, 800, 560), 192);
    Assert(new Rectangle(-800, 0, 800, 560).Contains(moved));
    Assert(moved.Width > 0 && moved.Height > 0);
});
static Task SidebarAdaptation() => Sync(() =>
{
    Assert(ResponsiveGeometry.Layout(new Size(1280, 720), 96).SidebarWidth == 244);
    Assert(ResponsiveGeometry.Layout(new Size(1024, 720), 96).SidebarWidth == 220);
    Assert(ResponsiveGeometry.Layout(new Size(800, 600), 96).SidebarWidth == 192);
    ResponsiveMetrics narrow = ResponsiveGeometry.Layout(new Size(640, 480), 96);
    Assert(narrow.CommandMenu && narrow.SidebarWidth == 0 && narrow.StackedFooter);
    ResponsiveMetrics shortWindow = ResponsiveGeometry.Layout(new Size(1280, 320), 96);
    Assert(shortWindow.CommandMenu && shortWindow.SidebarWidth == 0 && shortWindow.ShortHeight);
});
static Task DpiLayoutModes() => Sync(() =>
{
    foreach (int dpi in new[] { 96, 120, 144, 192, 240, 288 })
    {
        ResponsiveMetrics full = ResponsiveGeometry.Layout(new Size(ResponsiveGeometry.Scale(1280, dpi), ResponsiveGeometry.Scale(720, dpi)), dpi);
        Assert(full.SidebarWidth == ResponsiveGeometry.Scale(244, dpi) && !full.CommandMenu && !full.StackedHeader);
        ResponsiveMetrics medium = ResponsiveGeometry.Layout(new Size(ResponsiveGeometry.Scale(1000, dpi), ResponsiveGeometry.Scale(720, dpi)), dpi);
        Assert(medium.StackedHeader && !medium.CommandMenu);
        ResponsiveMetrics narrow = ResponsiveGeometry.Layout(new Size(ResponsiveGeometry.Scale(640, dpi), ResponsiveGeometry.Scale(480, dpi)), dpi);
        Assert(narrow.CommandMenu && narrow.SidebarWidth == 0);
    }
});
static Task RevealSmallActions() => Sync(() =>
{
    Assert(ResponsiveGeometry.Reveal(200, 100, 50, 40, 600) == 50);
    Assert(ResponsiveGeometry.Reveal(0, 100, 200, 40, 600) == 140);
    Assert(ResponsiveGeometry.Reveal(100, 100, 120, 40, 600) == 100);
    Assert(ResponsiveGeometry.Reveal(0, 100, 700, 40, 600) == 500);
});
static Task RevealLargeViewers() => Sync(() =>
{
    Assert(ResponsiveGeometry.Reveal(200, 100, 0, 600, 800) == 200);
    Assert(ResponsiveGeometry.Reveal(0, 100, 200, 600, 1000) == 200);
    Assert(ResponsiveGeometry.Reveal(650, 100, 0, 600, 1000) == 500);
    Assert(ResponsiveGeometry.Reveal(100, 0, 100, 40, 200) <= 199);
});

static Task HeaderNaturalSizes() => Sync(() =>
{
    HeaderArrangement header = HeaderGeometry.Arrange(1248, new(220, 54), new(320, 48), new(100, 40), new(132, 40), 8, false, false);
    Assert(!header.Stacked && !header.CommandMenu);
    Assert(header.Tools == new Size(320, 48) && header.Captions == new Size(132, 40));
});
static Task HeaderMeasuredOverflow() => Sync(() =>
{
    HeaderArrangement narrow = HeaderGeometry.Arrange(670, new(240, 54), new(320, 48), new(100, 40), new(132, 40), 8, false, false);
    Assert(narrow.Stacked && !narrow.CommandMenu && narrow.Tools == new Size(320, 48));
    HeaderArrangement largerText = HeaderGeometry.Arrange(900, new(300, 80), new(500, 64), new(140, 56), new(132, 40), 8, false, false);
    Assert(largerText.Stacked && largerText.Tools.Height == 64);
});
static Task HeaderMenuFallback() => Sync(() =>
{
    HeaderArrangement header = HeaderGeometry.Arrange(500, new(180, 50), new(600, 64), new(100, 40), new(132, 40), 8, false, false);
    Assert(header.CommandMenu && !header.Stacked && header.Tools == new Size(100, 40));
    header = HeaderGeometry.Arrange(300, new(48, 48), new(300, 48), new(100, 40), new(108, 32), 8, true, false);
    Assert(header.CommandMenu && !header.Stacked && header.Tools.Width + header.Captions.Width + 48 + 16 <= 300);
});
static Task HeaderDpiCycles() => Sync(() =>
{
    foreach (int dpi in new[] { 96, 120, 144, 168, 192, 240, 288, 384 })
    foreach (int logical in new[] { 1280, 1100, 1099, 1080, 1079, 900, 800, 760, 759, 640, 480, 640, 1280 })
    {
        int S(int value) => ResponsiveGeometry.Scale(value, dpi);
        ResponsiveMetrics layout = ResponsiveGeometry.Layout(new(S(logical), S(720)), dpi);
        int available = S(logical) - 2 * layout.PaddingHorizontal;
        HeaderArrangement header = HeaderGeometry.Arrange(available, new(S(layout.ShowBrandLabel ? 220 : 48), S(54)),
            new(S(320), S(48)), new(S(100), S(40)), new(layout.CaptionWidth * 3 + S(12), layout.CaptionWidth),
            S(8), layout.CommandMenu, layout.StackedHeader);
        Assert(header.Tools.Width + S(8) <= available, $"Toolbar clips at {logical}, {dpi} DPI.");
        Assert(header.Tools.Height == S(header.CommandMenu ? 40 : 48));
        if (!header.Stacked) Assert((layout.ShowBrandLabel ? S(220) : S(48)) + header.Tools.Width + header.Captions.Width + S(16) <= available);
    }
});
static Task IconTextAlignment() => Sync(() =>
{
    Rectangle content = new(12, 7, 180, 26);
    foreach (int alignment in new[] { -1, 0, 1 })
    {
        IconContent parts = IconGeometry.Arrange(content, new(100, 18), 18, 9, alignment);
        Assert(content.Contains(parts.Icon) && content.Contains(parts.Text));
        Assert(parts.Icon.Right + 9 == parts.Text.Left && parts.Text.Width == 100);
        Assert(parts.Icon.Top + parts.Icon.Height / 2 == content.Top + content.Height / 2);
        if (alignment == -1) Assert(parts.Icon.Left == content.Left);
        if (alignment == 0) Assert(parts.Icon.Left - content.Left == (content.Width - 127) / 2);
        if (alignment == 1) Assert(parts.Text.Right == content.Right);
    }
});
static Task IconTinyBounds() => Sync(() =>
{
    foreach (int width in new[] { 0, 1, 2, 8, 18, 20, 100 })
    foreach (int height in new[] { 0, 1, 2, 8, 18, 40 })
    foreach (int alignment in new[] { -1, 0, 1 })
    {
        Rectangle content = new(5, 7, width, height);
        IconContent parts = IconGeometry.Arrange(content, new(100, 20), 18, 9, alignment);
        foreach (Rectangle part in new[] { parts.Icon, parts.Text })
            Assert(part.Left >= content.Left && part.Top >= content.Top && part.Right <= content.Right && part.Bottom <= content.Bottom);
        Assert(parts.Icon.Right <= parts.Text.Left);
    }
});
static Task IconPreferredSizes() => Sync(() =>
{
    foreach (int dpi in new[] { 96, 120, 144, 192, 288, 384 })
    {
        int S(int value) => ResponsiveGeometry.Scale(value, dpi);
        Size text = new(S(172), S(20));
        Size preferred = IconGeometry.Measure(text, S(18), S(9));
        IconContent parts = IconGeometry.Arrange(new(Point.Empty, preferred), text, S(18), S(9), 0);
        Assert(parts.Text.Width == text.Width && parts.Icon.Width == S(18));
        Assert(preferred.Width == text.Width + S(18) + S(9));
    }
    Assert(IconGeometry.Measure(Size.Empty, 18, 9) == new Size(18, 18));
});
static Task IconVectorDefinitions() => Sync(() =>
{
    foreach (UiIcon icon in Enum.GetValues<UiIcon>().Where(icon => icon != UiIcon.None))
    {
        Assert(UiIconVectors.Shapes.TryGetValue(icon, out IconPrimitive[]? shapes) && shapes.Length > 0, icon + " is missing.");
        foreach (IconPrimitive shape in shapes!)
        {
            Assert(shape.Points.All(p => float.IsFinite(p) && p >= 0 && p <= 24), icon + " has invalid coordinates.");
            if (shape.Kind is IconPrimitiveKind.Ellipse or IconPrimitiveKind.Rectangle)
                Assert(shape.Points.Length == 4 && shape.Points[0] + shape.Points[2] <= 24 && shape.Points[1] + shape.Points[3] <= 24);
            else if (shape.Kind == IconPrimitiveKind.Bezier) Assert(shape.Points.Length >= 8 && (shape.Points.Length - 2) % 6 == 0);
            else Assert(shape.Points.Length >= 4 && shape.Points.Length % 2 == 0);
        }
    }
});

static Task CleanupIdleServices() => Sync(() =>
{
    var queried = new List<string>();
    string? reason = CleanupServiceGate.BlockReason(["wuauserv","BITS"],name => { queried.Add(name); return 1; },() => false);
    Assert(reason == null && queried.SequenceEqual(new[] { "wuauserv","BITS" }));
});
static Task CleanupActiveServices() => Sync(() =>
{
    foreach (uint state in new uint[] { 2,3,4,5,6,7,0,uint.MaxValue })
    {
        bool restarted = false;
        string? reason = CleanupServiceGate.BlockReason(["wuauserv","BITS"],name => name == "BITS" ? state : 1,() => { restarted = true; return false; });
        Assert(reason != null && reason.Contains("BITS") && !restarted);
    }
});
static Task CleanupUnavailableServices() => Sync(() =>
{
    foreach (Exception failure in new Exception[] { new System.ComponentModel.Win32Exception(5),new IOException("Unavailable"),
        new UnauthorizedAccessException(),new System.Security.SecurityException() })
        Assert(CleanupServiceGate.BlockReason(["wuauserv","BITS"],_ => throw failure,() => false) != null);
});
static Task CleanupPendingRestart() => Sync(() =>
{
    Assert(CleanupServiceGate.BlockReason(["wuauserv","BITS"],_ => 1,() => true) != null);
    Assert(CleanupServiceGate.BlockReason([],_ => throw new InvalidOperationException(),() => throw new InvalidOperationException()) == null);
});
static async Task CleanupServiceNames()
{
    bool queried = false;
    await Throws<InvalidDataException>(() => Sync(() => CleanupServiceGate.BlockReason(["wuauserv","arbitrary-service"],_ => { queried = true; return 1; },() => false)));
    Assert(!queried);
}
static Task MaintenanceRowBounds() => Sync(() =>
{
    foreach (int dpi in new[] { 96,120,144,168,192,240,288,384 })
    foreach (int logicalWidth in new[] { 520,640,759,760,800,960,1280,1920 })
    foreach (int logicalTextHeight in new[] { 20,40,120,500 })
    {
        int S(int value) => DetailListGeometry.Scale(value,dpi);
        int width = S(logicalWidth);
        DetailRowBounds row = DetailListGeometry.Arrange(width,S(430),S(40),S(logicalTextHeight),dpi);
        var bounds = new Rectangle(0,0,width,row.Height);
        Assert(bounds.Contains(row.Action) && bounds.Contains(row.Description));
        Assert(!row.Action.IntersectsWith(row.Description) && row.Action.Height >= S(40));
        Assert(row.Action.Width >= S(430) && row.Description.Height >= S(logicalTextHeight));
    }
});
static Task MaintenanceStackedRows() => Sync(() =>
{
    foreach (int dpi in new[] { 96,120,144,192,288,384 })
    {
        int S(int value) => DetailListGeometry.Scale(value,dpi);
        DetailRowBounds narrow = DetailListGeometry.Arrange(S(520),S(430),S(40),S(200),dpi);
        DetailRowBounds wide = DetailListGeometry.Arrange(S(1000),S(430),S(40),S(80),dpi);
        Assert(narrow.Action.Left == narrow.Description.Left && narrow.Description.Top >= narrow.Action.Bottom+S(12));
        Assert(narrow.Description.Width == S(520) && narrow.Height >= S(240));
        Assert(wide.Action.Top == wide.Description.Top && wide.Description.Left >= wide.Action.Right+S(12));
    }
});
static Task MaintenanceWheelStep() => Sync(() =>
{
    var wheel = new WheelAccumulator(); int value = 0;
    foreach (int delta in new[] { -40,-40,-40 }) value = wheel.Move(value,2000,delta,UiPalette.MaintenanceWheelStep,400,3,false).Value;
    Assert(value > 0 && value < UiPalette.FieldHeight+UiPalette.SectionGap);
    Assert(!wheel.Move(0,2000,120,UiPalette.MaintenanceWheelStep,400,3,false).Accepted);
});

static Task FineWheelInput() => Sync(() =>
{
    var wheel = new WheelAccumulator(); int value = 400;
    foreach (int expected in new[] { 400, 400, 496 })
    {
        WheelMove move = wheel.Move(value, 1000, -40, 32, 200, 3, false);
        Assert(move.Accepted && move.Value == expected); value = move.Value;
    }
    Assert(wheel.Move(value, 1000, 120, 32, 200, 3, false).Value == 400);
});
static Task WheelSettings() => Sync(() =>
{
    var wheel = new WheelAccumulator();
    Assert(!wheel.Move(200, 1000, -120, 24, 180, 0, false).Accepted);
    Assert(wheel.Move(200, 1000, -120, 24, 180, 1, false).Value == 224);
    Assert(wheel.Move(200, 1000, -120, 24, 180, -1, false).Value == 380);
});
static Task WheelEdgeHandoff() => Sync(() =>
{
    var inner = new WheelAccumulator(); var outer = new WheelAccumulator();
    Assert(!inner.Move(0, 100, 120, 32, 50, 3, false).Accepted);
    Assert(outer.Move(200, 1000, 120, 32, 200, 3, false).Value == 104);
    Assert(!inner.Move(100, 100, -120, 32, 50, 3, false).Accepted);
    Assert(inner.Move(0, 100, -40, 32, 50, 3, false).Accepted);
    Assert(!inner.Move(0, 0, -120, 32, 50, 3, false).Accepted);
});
static Task WheelAxisChange() => Sync(() =>
{
    var wheel = new WheelAccumulator();
    Assert(wheel.Move(100, 1000, -80, 24, 200, 3, false).Value == 100);
    Assert(wheel.Move(100, 1000, -40, 24, 200, 3, true).Value == 100);
    Assert(wheel.Move(100, 1000, -80, 24, 200, 3, true).Value == 172);
});
static Task WheelLargeValues() => Sync(() =>
{
    var wheel = new WheelAccumulator();
    Assert(wheel.Move(1, int.MaxValue, int.MinValue, int.MaxValue, int.MaxValue, int.MaxValue, false).Value == int.MaxValue);
    Assert(wheel.Move(int.MaxValue-1, int.MaxValue, int.MaxValue, int.MaxValue, int.MaxValue, int.MaxValue, false).Value == 0);
});
static Task PopupWorkingAreas() => Sync(() =>
{
    Rectangle work = new(-1600, 0, 1600, 860);
    PopupPlacement down = DropDownGeometry.Place(new(-1400, 100, 220, 40), work, 400, 32, 50, 6, 8);
    Assert(work.Contains(down.Bounds) && down.Bounds.Top == 140 && down.VisibleItems == 8);
    PopupPlacement up = DropDownGeometry.Place(new(-1400, 760, 220, 40), work, 400, 32, 50, 6, 8);
    Assert(work.Contains(up.Bounds) && up.Bounds.Bottom == 760);
    foreach (Rectangle area in new[] { new Rectangle(0,0,640,400), new Rectangle(50,60,1,1), new Rectangle(-800,-600,800,560) })
    foreach (Rectangle anchor in new[] { new Rectangle(area.Left,area.Top,180,40), new Rectangle(area.Right-30,area.Bottom-40,180,40) })
        Assert(area.Contains(DropDownGeometry.Place(anchor,area,900,64,100,12,8).Bounds));
});
static Task PopupRowHitTesting() => Sync(() =>
{
    Assert(DropDownGeometry.HitRow(-1, 64, 32, 10) == -1);
    Assert(DropDownGeometry.HitRow(0, 64, 32, 10) == 2);
    Assert(DropDownGeometry.HitRow(31, 64, 32, 10) == 2);
    Assert(DropDownGeometry.HitRow(32, 64, 32, 10) == 3);
    Assert(DropDownGeometry.HitRow(320, 64, 32, 10) == -1);
});
static Task GridVariableRows() => Sync(() =>
{
    ScrollRow[] rows = [new(0,30),new(1,90),new(2,30),new(3,60)];
    Assert(GridScrollGeometry.Extent(rows) == 210);
    Assert(GridScrollGeometry.Offset(rows,2) == 120);
    Assert(GridScrollGeometry.RowAt(rows,40,90) == 1);
    int end = GridScrollGeometry.RowAt(rows,120,90);
    Assert(end == 2 && GridScrollGeometry.Offset(rows,end)+90 >= 210);
    ScrollRow[] uneven = [new(0,40),new(1,40),new(2,40),new(3,40)];
    int last = GridScrollGeometry.RowAt(uneven,60,100);
    Assert(last == 2 && GridScrollGeometry.Offset(uneven,last)+100 >= 160);
});
static Task GridRowBounds() => Sync(() =>
{
    ScrollRow[] eligible = [new(2,32),new(5,48),new(9,32)];
    Assert(GridScrollGeometry.RowAt(eligible,-999,32) == 2);
    Assert(GridScrollGeometry.RowAt(eligible,int.MaxValue,32) == 9);
    Assert(GridScrollGeometry.RowAt([],50,20) == -1);
    Assert(GridScrollGeometry.Extent([new(0,int.MaxValue),new(1,int.MaxValue)]) == int.MaxValue);
    Assert(GridScrollGeometry.Offset(eligible,-1) == 0);
});

static Task ExpandedPreferenceDefaults() => Sync(() =>
{
    Assert(PreferenceCatalog.All.Count == 34);
    Assert(PreferenceCatalog.All.Select(p => p.Id).Distinct(StringComparer.Ordinal).Count() == PreferenceCatalog.All.Count);
    Assert(PreferenceCatalog.All.Where(p => p.Recommended).Select(p => p.Id).Order().SequenceEqual(new[] { "capture","menus","suggestions" }));
    foreach (string original in new[] { "menus","capture","suggestions","animations","transparency","power","diagnostics" }) Assert(PreferenceCatalog.All.Any(p => p.Id == original));
});
static Task PreferenceTargetIntegrity() => Sync(() =>
{
    PreferenceValue[] values = PreferenceCatalog.All.SelectMany(p => p.Values).ToArray();
    Assert(values.Select(v => v.Path+"|"+v.Name).Distinct(StringComparer.OrdinalIgnoreCase).Count() == values.Length,"Preferences overlap and could overwrite each other's snapshots.");
    foreach (PreferenceValue value in values)
    {
        Assert(value.Path.Length > 0 && value.Name.Length > 0 && value.Kind is "DWord" or "String");
        Assert(value.Kind == "DWord" ? value.Value is int : value.Value is string);
        Assert(!value.Path.Contains("Windows Defender",StringComparison.OrdinalIgnoreCase) && !value.Path.Contains("Firewall",StringComparison.OrdinalIgnoreCase));
    }
});
static Task PreferenceConfiguredValues() => Sync(() =>
{
    PreferenceValue menu = PreferenceCatalog.All.Single(p => p.Id == "menus").Values.Single();
    Assert(Equals(PreferenceCatalog.ResolveValue(menu,new() { MenuDelayMilliseconds = 75 }),"75"));
    PreferenceValue capture = PreferenceCatalog.All.Single(p => p.Id == "capture").Values[0];
    Assert(Equals(PreferenceCatalog.ResolveValue(capture,new()),0));
});
static Task RepairInventoryIntegrity() => Sync(() =>
{
    string[] allowed = ["dism.exe","sfc.exe","chkdsk.exe","defrag.exe","netsh.exe","ipconfig.exe","powercfg.exe","w32tm.exe","wevtutil.exe"];
    Assert(RepairCatalog.All.Count == 23 && RepairCatalog.All.Select(a => a.Id).Distinct().Count() == 23);
    foreach (RepairDefinition action in RepairCatalog.All)
    {
        Assert(allowed.Contains(action.Executable) && action.Arguments.Length > 0);
        Assert(!action.Arguments.Any(a => a.Contains("ResetBase",StringComparison.OrdinalIgnoreCase) || a.Equals("/forcerestart",StringComparison.OrdinalIgnoreCase)));
        if (action.ReadOnly) Assert(!action.Restart);
    }
});
static async Task RepairDriveArguments()
{
    RepairDefinition scan = RepairCatalog.All.Single(a => a.Id == "disk-scan");
    Assert(RepairCatalog.ArgumentsFor(scan,"D:").SequenceEqual(new[] { "D:","/scan" }));
    await Throws<InvalidDataException>(() => Sync(() => RepairCatalog.ArgumentsFor(scan,"C: & shutdown")));
}
static Task UninstallParsing() => Sync(() =>
{
    UninstallCommand quoted = AppInventoryPolicy.Parse("\"C:\\Program Files\\Example\\uninstall.exe\" /uninstall \"keep quoted argument\"");
    Assert(quoted.Executable == @"C:\Program Files\Example\uninstall.exe" && quoted.Arguments == "/uninstall \"keep quoted argument\"");
    UninstallCommand unquoted = AppInventoryPolicy.Parse(@"C:\Folder.exe-name\Example\remove.exe /remove");
    Assert(unquoted.Executable == @"C:\Folder.exe-name\Example\remove.exe" && unquoted.Arguments == "/remove");
});
static async Task UninstallHostProtection()
{
    foreach (string command in new[] { @"C:\Windows\System32\cmd.exe /c delete",@"C:\Windows\System32\WindowsPowerShell\v1.0\powershell.exe -command x",@"\\server\share\remove.exe /x",@"relative\remove.exe",@"C:\remove.cmd", "\"C:\\broken.exe" })
        await Throws<InvalidDataException>(() => Sync(() => AppInventoryPolicy.Parse(command)));
}
static Task InstalledAppProtection() => Sync(() =>
{
    Assert(AppInventoryPolicy.ProtectionReason("Example",true) != null);
    Assert(AppInventoryPolicy.ProtectionReason("Shared framework",false,true) != null);
    Assert(AppInventoryPolicy.ProtectionReason("Microsoft Visual C++ Redistributable",false) != null);
    Assert(AppInventoryPolicy.ProtectionReason("Windows Desktop Runtime",false) != null);
    Assert(AppInventoryPolicy.ProtectionReason("Bitdefender Total Security",false) != null);
    Assert(AppInventoryPolicy.ProtectionReason("Realtek Audio Driver",false) != null);
    Assert(AppInventoryPolicy.ProtectionReason("Example Editor",false) == null);
});
static Task ProgressStepFractions() => Sync(() =>
{
    var state = new OperationProgress(); state.SetSteps(0,4); state.SetStepPercent(50); Assert(state.Percent == 12.5 && state.Caption == "12%");
    state.SetStepPercent(20); Assert(state.Percent == 12.5);
    state.SetSteps(1,4); Assert(state.Percent == 25); state.SetStepPercent(100); Assert(state.Percent < 50);
    state.SetSteps(2,4); Assert(state.Percent == 50); state.Complete(); Assert(state.Percent == 100 && state.Caption == "100%");
});
static Task ProgressUnknownAndComplete() => Sync(() =>
{
    var state = new OperationProgress(); Assert(state.Percent == null && state.IsIndeterminate && state.Caption == "Working…");
    state.SetStepPercent(double.NaN); Assert(state.Percent == null);
    state.SetSteps(0,2); Assert(state.Caption == "0%" && state.IsIndeterminate);
    state.Complete(); Assert(!state.IsIndeterminate && state.Percent == 100); state.Reset(); Assert(state.Percent == null);
});
static Task NativePercentParsing() => Sync(() =>
{
    Assert(OperationProgress.ParsePercent("[====== 37.5% ====]") == 37.5);
    Assert(OperationProgress.ParsePercent("Progress: 37,5 %") == 37.5);
    Assert(OperationProgress.ParsePercent("100% complete") == 100);
    foreach (string text in new[] { "-1%","101%","1100%","No estimate","NaN%" }) Assert(OperationProgress.ParsePercent(text) == null,text);
});
static Task FilteredCacheScan() => Sync(() =>
{
    using var fixture = new Fixture(); string root = Path.Combine(fixture.Root,"cache"); Directory.CreateDirectory(root);
    File.WriteAllText(Path.Combine(root,"thumbcache_1.db"),"cache"); File.WriteAllText(Path.Combine(root,"settings.json"),"keep");
    TempScan scan = TempScanner.Scan([root],new(),CancellationToken.None,DateTime.UtcNow.AddDays(12),
        includeFile:path => Path.GetFileName(path).StartsWith("thumbcache_",StringComparison.Ordinal) && path.EndsWith(".db",StringComparison.Ordinal));
    Assert(scan.Files.Count == 1 && Path.GetFileName(scan.Files[0].FullPath) == "thumbcache_1.db");
    Assert(File.Exists(Path.Combine(root,"settings.json")));
});
static Task ScanProgressCounts() => Sync(() =>
{
    using var fixture = new Fixture(); string root = Path.Combine(fixture.Root,"cache"); Directory.CreateDirectory(root);
    for (int i=0;i<300;i++) File.WriteAllText(Path.Combine(root,i+".tmp"),"cache");
    var reports = new List<ScanProgress>();
    TempScan scan = TempScanner.Scan([root],new(),CancellationToken.None,DateTime.UtcNow.AddDays(12),progress:new InlineProgress<ScanProgress>(reports.Add));
    Assert(reports.Count >= 2 && reports[^1].Visited == 300 && reports[^1].Eligible == scan.Files.Count);
});
static async Task InspectHistory()
{
    using var fixture = new Fixture(); ChangeRun valid = await fixture.Engine.RunAsync("valid",[fixture.Step("one",10)]);
    File.WriteAllText(Path.Combine(fixture.Store.DirectoryPath,Guid.NewGuid().ToString("N")+".json"),"broken");
    JournalInspection result = fixture.Store.Inspect(); Assert(result.Runs.Count == 1 && result.Runs[0].Id == valid.Id && result.Problems.Count == 1);
}
static async Task InspectStillBlocksMutation()
{
    using var fixture = new Fixture(); Directory.CreateDirectory(fixture.Store.DirectoryPath);
    File.WriteAllText(Path.Combine(fixture.Store.DirectoryPath,Guid.NewGuid().ToString("N")+".json"),"broken");
    Assert(fixture.Store.Inspect().Problems.Count == 1);
    await Throws<JsonException>(() => fixture.Engine.RunAsync("blocked",[fixture.Step("one",10)])); Assert(fixture.Undo.Value == 5);
}
static async Task IncompleteHistory()
{
    using var fixture = new Fixture(); Directory.CreateDirectory(fixture.Store.DirectoryPath); string id = Guid.NewGuid().ToString("N");
    File.WriteAllText(Path.Combine(fixture.Store.DirectoryPath,id+".json"),"{\"id\":\""+id+"\",\"formatVersion\":1,\"entries\":null}");
    Assert(fixture.Store.Inspect().Problems.Count == 1);
    await Throws<InvalidDataException>(() => Sync(() => fixture.Store.Load(id)));
}

static async Task DurableBeforeApply()
{
    using var f = new Fixture();
    var step = f.Step("one", 12);
    step.BeforeApply = () =>
    {
        ChangeRun record = f.Store.List().Single();
        Assert(record.Entries.Single().Status == EntryStatus.Applying);
        Assert(JsonDefaults.Read<int>(record.Entries.Single().State) == 5);
    };
    ChangeRun run = await f.Engine.RunAsync("tuning", [step]);
    Assert(f.Undo.Value == 12 && run.Entries.Single().Status == EntryStatus.Applied);
    Assert(f.Store.Load(run.Id).FinishedUtc != null);
}
static async Task CaptureFailure()
{
    using var f = new Fixture(); var step = f.Step("one", 12); step.FailCapture = true;
    ChangeRun run = await f.Engine.RunAsync("capture", [step]);
    Assert(f.Undo.Value == 5 && step.ApplyCount == 0 && run.HasErrors && !run.HasPendingUndo);
}
static async Task JournalFailure()
{
    using var f = new Fixture(); var step = f.Step("one", 12); step.InvalidSnapshot = true;
    await Throws<InvalidOperationException>(() => f.Engine.RunAsync("disk", [step]));
    Assert(step.ApplyCount == 0 && f.Undo.Value == 5);
}
static async Task Rollback()
{
    using var f = new Fixture(); var step = f.Step("one", 12); step.FailApply = true;
    ChangeRun run = await f.Engine.RunAsync("rollback", [step]);
    Assert(f.Undo.Value == 5 && run.Entries.Single().Status == EntryStatus.RolledBack && !run.HasPendingUndo);
}
static async Task FailedRollback()
{
    using var f = new Fixture(); var step = f.Step("one", 12); step.FailApply = true; f.Undo.Fail = true;
    ChangeRun run = await f.Engine.RunAsync("rollback fails", [step]);
    Assert(run.Entries.Single().Status == EntryStatus.RecoveryRequired && f.Undo.Value == 12);
    await Throws<InvalidOperationException>(() => f.Engine.RunAsync("blocked", [f.Step("two", 18)]));
    f.Undo.Fail = false; await f.Engine.RestoreAsync(run.Id); Assert(f.Undo.Value == 5);
}
static async Task CrashRecovery()
{
    using var f = new Fixture();
    var run = new ChangeRun { MachineId = "pc", UserSid = "user", Title = "interrupted", Entries =
        [new ChangeEntry { Id = "one", Kind = "fake", Title = "fake", CanUndo = true, State = JsonDefaults.Element(5), Status = EntryStatus.Applying }] };
    f.Store.Save(run); f.Undo.Value = 99;
    await Throws<InvalidOperationException>(() => f.Engine.RunAsync("blocked", [f.Step("two", 15)]));
    await f.Engine.RestoreAsync(run.Id); Assert(f.Undo.Value == 5 && !f.Store.Load(run.Id).HasPendingUndo);
}
static async Task ReverseRestore()
{
    using var f = new Fixture(); ChangeRun run = await f.Engine.RunAsync("multiple", [f.Step("one", 10), f.Step("two", 20)]);
    await f.Engine.RestoreAsync(run.Id);
    Assert(f.Undo.Order.SequenceEqual(new[] { "two", "one" }) && f.Undo.Value == 5);
}
static async Task LatestFirst()
{
    using var f = new Fixture(); ChangeRun first = await f.Engine.RunAsync("first", [f.Step("one", 10)]);
    ChangeRun second = await f.Engine.RunAsync("second", [f.Step("two", 20)]);
    await Throws<InvalidOperationException>(() => f.Engine.RestoreAsync(first.Id));
    await f.Engine.RestoreAsync(second.Id); Assert(f.Undo.Value == 10);
    await f.Engine.RestoreAsync(first.Id); Assert(f.Undo.Value == 5);
}
static async Task IdentityGuard()
{
    using var f = new Fixture(); ChangeRun run = await f.Engine.RunAsync("identity", [f.Step("one", 10)]);
    await Throws<InvalidOperationException>(() => new ChangeEngine(f.Store, f.Undo, "other", "user").RestoreAsync(run.Id));
    await Throws<InvalidOperationException>(() => new ChangeEngine(f.Store, f.Undo, "pc", "other").RestoreAsync(run.Id));
    Assert(f.Undo.Value == 10);
}
static async Task ClockChange()
{
    using var f = new Fixture(); ChangeRun first = await f.Engine.RunAsync("first", [f.Step("one", 10)]);
    ChangeRun second = await f.Engine.RunAsync("second", [f.Step("two", 20)]);
    second.StartedUtc = first.StartedUtc.AddHours(-2); f.Store.Save(second);
    Assert(f.Store.List().First().Id == second.Id);
    await f.Engine.RestoreAsync(second.Id); await f.Engine.RestoreAsync(first.Id); Assert(f.Undo.Value == 5);
}
static async Task StopBetweenSteps()
{
    using var f = new Fixture(); using var cancel = new CancellationTokenSource();
    var first = f.Step("one", 10); first.BeforeApply = cancel.Cancel; var second = f.Step("two", 20);
    ChangeRun run = await f.Engine.RunAsync("stop", [first, second], cancellationToken: cancel.Token);
    Assert(f.Undo.Value == 10 && second.ApplyCount == 0 && run.WasCancelled && run.HasPendingUndo);
}
static async Task NoFakeUndo()
{
    using var f = new Fixture(); var step = f.Step("irreversible", 10); step.Undoable = false;
    ChangeRun run = await f.Engine.RunAsync("delete", [step]);
    Assert(!run.HasPendingUndo); await Throws<InvalidOperationException>(() => f.Engine.RestoreAsync(run.Id));
    run.Entries[0].Status = EntryStatus.Applying; run.FinishedUtc = null; f.Store.Save(run);
    _ = await f.Engine.RunAsync("new batch allowed", [f.Step("two", 11)]);
}
static async Task RetryRestore()
{
    using var f = new Fixture(); ChangeRun run = await f.Engine.RunAsync("restore errors", [f.Step("one", 10), f.Step("two", 20)]);
    f.Undo.FailId = "two"; await f.Engine.RestoreAsync(run.Id);
    ChangeRun restored = f.Store.Load(run.Id);
    Assert(restored.Entries[0].Status == EntryStatus.Applied && restored.Entries[1].Status == EntryStatus.RestoreFailed);
    f.Undo.FailId = null; await f.Engine.RestoreAsync(run.Id); Assert(!f.Store.Load(run.Id).HasPendingUndo && f.Undo.Value == 5);
}
static async Task DuplicateActions()
{
    using var f = new Fixture(); await Throws<InvalidOperationException>(() => f.Engine.RunAsync("duplicate", [f.Step("one", 10), f.Step("one", 20)]));
    Assert(f.Undo.Value == 5 && f.Store.List().Count == 0);
}
static Task CorruptJournal() => Sync(() =>
{
    using var f = new Fixture(); Directory.CreateDirectory(f.Store.DirectoryPath);
    File.WriteAllText(Path.Combine(f.Store.DirectoryPath, Guid.NewGuid().ToString("N") + ".json"), "invalid");
    bool failed = false; try { _ = f.Store.List(); } catch (JsonException) { failed = true; } Assert(failed);
});
static Task JournalPathGuard() => Sync(() =>
{
    using var f = new Fixture(); bool failed = false;
    try { _ = f.Store.Load("../outside"); } catch (InvalidDataException) { failed = true; } Assert(failed);
});
static Task ScanEligibility() => Sync(() =>
{
    using var f = new Fixture(); string root = Path.Combine(f.Root, "temp"); Directory.CreateDirectory(root);
    string old = Path.Combine(root, "old.tmp"), fresh = Path.Combine(root, "fresh.tmp"), readOnly = Path.Combine(root, "readonly.tmp");
    File.WriteAllText(old, "old"); File.WriteAllText(fresh, "fresh"); File.WriteAllText(readOnly, "readonly");
    DateTime testNow = DateTime.UtcNow.AddDays(12); File.SetLastWriteTimeUtc(fresh, testNow);
    File.SetAttributes(readOnly, FileAttributes.ReadOnly);
    try
    {
        TempScan scan = TempScanner.Scan([root], new(), CancellationToken.None, testNow);
        Assert(scan.Files.Count == 1 && scan.Files[0].FullPath == old && scan.Bytes == 3);
        Assert(TempScanner.IsInside(old, root)); Assert(!TempScanner.IsInside(root + "-other/data", root));
    }
    finally { File.SetAttributes(readOnly, FileAttributes.Normal); }
});
static Task SkipLinks() => Sync(() =>
{
    using var f = new Fixture(); string root = Path.Combine(f.Root, "temp"), outside = Path.Combine(f.Root, "outside");
    Directory.CreateDirectory(root); Directory.CreateDirectory(outside); string target = Path.Combine(outside, "keep.txt"); File.WriteAllText(target, "keep");
    Directory.CreateSymbolicLink(Path.Combine(root, "junction"), outside); File.CreateSymbolicLink(Path.Combine(root, "link"), target);
    TempScan scan = TempScanner.Scan([root], new(), CancellationToken.None, DateTime.UtcNow.AddDays(12));
    Assert(scan.Files.Count == 0 && scan.Skipped >= 2 && File.ReadAllText(target) == "keep");
    Assert(TempScanner.Scan([Path.Combine(root, "junction")], new(), CancellationToken.None, DateTime.UtcNow.AddDays(12)).Files.Count == 0);
});
static Task ScanBound() => Sync(() =>
{
    using var f = new Fixture(); string root = Path.Combine(f.Root, "temp"); Directory.CreateDirectory(root);
    for (int i = 0; i < 150; i++) File.WriteAllText(Path.Combine(root, i + ".tmp"), "a");
    TempScan scan = TempScanner.Scan([root], new() { MaxScannedFiles = 100 }, CancellationToken.None, DateTime.UtcNow.AddDays(12));
    Assert(scan.Truncated && scan.Files.Count <= 100);
});
static async Task CancelScan()
{
    using var f = new Fixture(); using var cancel = new CancellationTokenSource(); cancel.Cancel();
    await Throws<OperationCanceledException>(() => Sync(() => _ = TempScanner.Scan([f.Root], new(), cancel.Token)));
}
static Task ConfigurationValidation() => Sync(() =>
{
    foreach (BoosterOptions invalid in new[] { new BoosterOptions { TempMinimumAgeDays = 0 }, new BoosterOptions { MenuDelayMilliseconds = -1 }, new BoosterOptions { MaxScanSeconds = int.MaxValue }, new BoosterOptions { PingSamples = 0 } })
    {
        bool failed = false; try { invalid.Validate(); } catch (InvalidDataException) { failed = true; } Assert(failed);
    }
});
static Task ConfigurationRoundTrip() => Sync(() =>
{
    using var f = new Fixture(); string path = Path.Combine(f.Root, "settings.json");
    BoosterOptions defaults = BoosterOptions.LoadOrCreate(path); Assert(defaults.TempMinimumAgeDays == 7);
    defaults.MenuDelayMilliseconds = 75; File.WriteAllText(path, JsonSerializer.Serialize(defaults, JsonDefaults.Options));
    Assert(BoosterOptions.LoadOrCreate(path).MenuDelayMilliseconds == 75);
});

sealed class InlineProgress<T>(Action<T> action) : IProgress<T> { public void Report(T value) => action(value); }

sealed class Fixture : IDisposable
{
    public string Root { get; } = Path.Combine(Path.GetTempPath(), "BoosterX-check-" + Guid.NewGuid().ToString("N"));
    public RunStore Store { get; }
    public FakeUndo Undo { get; } = new();
    public ChangeEngine Engine { get; }
    public Fixture() { Directory.CreateDirectory(Root); Store = new(Path.Combine(Root, "Backups")); Engine = new(Store, Undo, "pc", "user"); }
    public FakeStep Step(string id, int value) => new(id, value, Undo);
    public void Dispose() { Directory.Delete(Root, recursive: true); }
}
sealed class FakeUndo : IUndoService
{
    public int Value { get; set; } = 5;
    public bool Fail { get; set; }
    public string? FailId { get; set; }
    public List<string> Order { get; } = [];
    public Task UndoAsync(ChangeEntry entry, CancellationToken cancellationToken)
    {
        if (Fail || FailId == entry.Id) throw new IOException("simulated restore error");
        Order.Add(entry.Id); Value = JsonDefaults.Read<int>(entry.State); return Task.CompletedTask;
    }
}
sealed class FakeStep(string id, int target, FakeUndo values) : IChangeStep
{
    public string Id => id;
    public string Title => id;
    public string Kind => "fake";
    public bool CanUndo => Undoable;
    public bool Undoable { get; set; } = true;
    public bool FailCapture { get; set; }
    public bool FailApply { get; set; }
    public bool InvalidSnapshot { get; set; }
    public int ApplyCount { get; private set; }
    public Action? BeforeApply { get; set; }
    public Task<JsonElement> CaptureAsync(CancellationToken cancellationToken)
    {
        if (FailCapture) throw new IOException("simulated capture failure");
        return Task.FromResult(InvalidSnapshot ? default : JsonDefaults.Element(values.Value));
    }
    public Task<StepOutcome> ApplyAsync(JsonElement state, CancellationToken cancellationToken)
    {
        BeforeApply?.Invoke(); ApplyCount++; values.Value = target;
        if (FailApply) throw new IOException("simulated partial mutation");
        return Task.FromResult(new StepOutcome("applied"));
    }
}
