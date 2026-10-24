using BoosterX.App.Controls;
using System.ComponentModel;

namespace BoosterX.App;

public partial class Form1
{
    private bool responsiveReady;
    private bool responsiveBusy;
    private bool responsiveQueued;
    private bool refitQueued;
    private bool rescaleQueued;
    private FormWindowState lastResponsiveWindowState;

    protected override void OnLoad(EventArgs e)
    {
        base.OnLoad(e);
        if (DesignMode || LicenseManager.UsageMode == LicenseUsageMode.Designtime) return;
        InitializeResponsiveLayout();
        FitToWorkingArea(initial: true);
        ApplyResponsiveLayout();
    }
    private void InitializeResponsiveLayout()
    {
        if (responsiveReady) return;
        responsiveReady = true;
        foreach (FlowLayoutPanel flow in Theme.Descendants(pageHost).OfType<FlowLayoutPanel>())
        {
            flow.SizeChanged += (_, _) => ConstrainFlow(flow);
            if (flow.Parent != null) flow.Parent.SizeChanged += (_, _) => ConstrainFlow(flow);
            ConstrainFlow(flow);
        }
        navigationPanel.SizeChanged += (_, _) => ResizeNavigation();
        footerLayout.SizeChanged += (_, _) => QueueResponsiveLayout();
        foreach (Control action in headerToolsFlow.Controls)
        {
            action.TextChanged += (_, _) => QueueResponsiveLayout();
            action.FontChanged += (_, _) => QueueResponsiveLayout();
            action.PaddingChanged += (_, _) => QueueResponsiveLayout();
        }
        AdjustComboHeights(); ScaleGridColumns();
    }
    protected override void OnClientSizeChanged(EventArgs e)
    {
        base.OnClientSizeChanged(e);
        QueueResponsiveLayout();
    }
    protected override void OnDpiChanged(DpiChangedEventArgs e)
    {
        base.OnDpiChanged(e);
        if (!responsiveReady) return;
        rescaleQueued = true;
        QueueResponsiveLayout(refit: true);
    }
    private void UpdateResponsiveWindowState()
    {
        bool restored = lastResponsiveWindowState == FormWindowState.Minimized && WindowState != FormWindowState.Minimized;
        lastResponsiveWindowState = WindowState;
        QueueResponsiveLayout(refit: restored);
    }
    protected override void OnResizeEnd(EventArgs e)
    {
        base.OnResizeEnd(e);
        if (!responsiveReady) return;
        FitToWorkingArea(); ApplyResponsiveLayout();
    }
    protected override void WndProc(ref Message m)
    {
        base.WndProc(ref m);
        // Display changes and SPI_SETWORKAREA cover resolution/taskbar changes without global event subscriptions.
        if (m.Msg == 0x007E || m.Msg == 0x001A && m.WParam.ToInt64() == 47) QueueResponsiveLayout(refit: true);
    }
    private void QueueResponsiveLayout(bool refit = false)
    {
        if (!responsiveReady || responsiveBusy || !IsHandleCreated || IsDisposed || Disposing) return;
        refitQueued |= refit;
        if (responsiveQueued) return;
        responsiveQueued = true;
        BeginInvoke((Action)(() =>
        {
            responsiveQueued = false;
            if (IsDisposed || Disposing) return;
            bool fit = refitQueued; refitQueued = false;
            if (rescaleQueued) { rescaleQueued = false; AdjustComboHeights(); ScaleGridColumns(); }
            if (fit) FitToWorkingArea();
            ApplyResponsiveLayout();
        }));
    }
    private void FitToWorkingArea(bool initial = false)
    {
        Rectangle working = Screen.FromControl(this).WorkingArea;
        Rectangle bounds = initial ? ResponsiveGeometry.Initial(working, DeviceDpi)
            : ResponsiveGeometry.Fit(Bounds, working, DeviceDpi);
        MinimumSize = ResponsiveGeometry.Minimum(working, DeviceDpi);
        MaximizedBounds = working;
        if (WindowState == FormWindowState.Normal && Bounds != bounds) Bounds = bounds;
    }
    private void ApplyResponsiveLayout()
    {
        if (!responsiveReady || responsiveBusy || ClientSize.Width <= 0 || ClientSize.Height <= 0) return;
        responsiveBusy = true;
        ResponsiveMetrics layout = ResponsiveGeometry.Layout(ClientSize, DeviceDpi);
        SuspendLayout(); headerLayout.SuspendLayout(); headerToolsFlow.SuspendLayout(); captionButtonsFlow.SuspendLayout();
        footerLayout.SuspendLayout(); bodyLayout.SuspendLayout();
        try
        {
            Padding = new Padding(layout.PaddingHorizontal, layout.PaddingVertical, layout.PaddingHorizontal, layout.PaddingVertical);
            int gap = LogicalToDeviceUnits(UiLayout.BodyGap);
            bodyLayout.Margin = new Padding(0, gap, 0, gap);
            bodyLayout.ColumnStyles[0].Width = layout.SidebarWidth + layout.SidebarGap;
            navigationPanelScrollHost.Visible = layout.SidebarWidth > 0;
            navigationPanelScrollHost.Margin = new Padding(0, 0, layout.SidebarGap, 0);
            brandLayout.Visible = layout.ShowBrandIcon;
            brandLabel.Visible = layout.ShowBrandLabel;
            brandLayout.Margin = new Padding(0, 0, gap, 0);
            foreach (RoundedButton button in new[] { minimizeWindowButton, maximizeWindowButton, closeWindowButton })
            {
                button.Size = new Size(layout.CaptionWidth, layout.CaptionWidth);
                button.Margin = new Padding(0, 0, LogicalToDeviceUnits(4), 0);
            }
            minimizeWindowButton.Visible = !layout.CompactCaptions;
            maximizeWindowButton.Visible = !layout.CompactCaptions;
            Size captions = MeasureHeaderRow(layout.CompactCaptions ? [closeWindowButton]
                : [minimizeWindowButton, maximizeWindowButton, closeWindowButton]);
            int brandWidth = layout.ShowBrandIcon ? LogicalToDeviceUnits(48)
                + (layout.ShowBrandLabel ? brandLabel.GetPreferredSize(Size.Empty).Width : 0) : 0;
            HeaderArrangement header = HeaderGeometry.Arrange(Math.Max(1, ClientSize.Width - Padding.Horizontal),
                new Size(brandWidth, 0), MeasureHeaderRow(elevateButton, logToggle), MeasureHeaderRow(responsiveMenuButton),
                captions, gap, layout.CommandMenu, layout.StackedHeader);
            responsiveMenuButton.Visible = header.CommandMenu;
            elevateButton.Visible = !header.CommandMenu;
            logToggle.Visible = !header.CommandMenu;
            brandSubtitleLabel.Visible = !header.CommandMenu && !header.Stacked;
            // Explicit natural sizes keep both toolbar buttons on ONE fully visible row.
            headerToolsFlow.MaximumSize = Size.Empty;
            headerToolsFlow.Size = header.Tools;
            captionButtonsFlow.Size = header.Captions;
            headerLayout.SetColumnSpan(brandLayout, header.Stacked ? 2 : 1);
            headerLayout.SetCellPosition(brandLayout, new TableLayoutPanelCellPosition(0, 0));
            bool toolsFill = header.Stacked || !layout.ShowBrandIcon;
            headerLayout.ColumnStyles[1].SizeType = SizeType.Absolute;
            headerLayout.ColumnStyles[1].Width = toolsFill ? 0 : header.Tools.Width + gap;
            headerLayout.ColumnStyles[2].SizeType = SizeType.Absolute;
            headerLayout.ColumnStyles[2].Width = header.Captions.Width;
            headerLayout.SetCellPosition(headerToolsFlow, new TableLayoutPanelCellPosition(toolsFill ? 0 : 1, header.Stacked ? 1 : 0));
            headerLayout.SetColumnSpan(headerToolsFlow, header.Stacked ? 3 : !layout.ShowBrandIcon ? 2 : 1);
            headerLayout.RowStyles[1].SizeType = header.Stacked ? SizeType.AutoSize : SizeType.Absolute;
            headerLayout.RowStyles[1].Height = 0;
            headerToolsFlow.Margin = new Padding(0, header.Stacked ? gap : 0, gap, 0);
            stopButton.Visible = !layout.ShortHeight;
            string stopText = layout.ShowBrandLabel ? UiLayout.StopText : UiLayout.CompactStopText;
            if (stopButton.Text != stopText)
            {
                stopButton.MinimumSize = Size.Empty;
                stopButton.Text = stopText;
            }
            bool footerStack = layout.StackedFooter || layout.ShortHeight;
            footerLayout.SetColumnSpan(statusLabel, footerStack ? 2 : 1);
            footerLayout.SetCellPosition(operationPercentLabel,new TableLayoutPanelCellPosition(footerStack ? 2 : 1,0));
            footerLayout.SetCellPosition(stopButton, new TableLayoutPanelCellPosition(footerStack ? 0 : 2, footerStack ? 1 : 0));
            footerLayout.SetColumnSpan(stopButton, footerStack ? 3 : 1);
            footerLayout.RowStyles[1].SizeType = footerStack && !layout.ShortHeight ? SizeType.AutoSize : SizeType.Absolute;
            footerLayout.RowStyles[1].Height = 0;
            statusLabel.Margin = new Padding(0, 0, footerStack ? 0 : gap, LogicalToDeviceUnits(4));
            int statusWidth = Math.Max(1, ClientSize.Width - Padding.Horizontal - statusLabel.Margin.Horizontal
                - operationPercentLabel.GetPreferredSize(Size.Empty).Width - operationPercentLabel.Margin.Horizontal
                - (footerStack ? 0 : stopButton.GetPreferredSize(Size.Empty).Width));
            statusLabel.MaximumSize = new Size(statusWidth, Math.Max(1, statusLabel.Font.Height * (layout.ShortHeight ? 1 : 2)));
            ResizeNavigation();
            foreach (FlowLayoutPanel flow in Theme.Descendants(pageHost).OfType<FlowLayoutPanel>()) ConstrainFlow(flow);
        }
        finally
        {
            bodyLayout.ResumeLayout(true); footerLayout.ResumeLayout(true);
            headerToolsFlow.ResumeLayout(true); captionButtonsFlow.ResumeLayout(true); headerLayout.ResumeLayout(true); ResumeLayout(true);
            responsiveBusy = false;
        }
    }
    private static Size MeasureHeaderRow(params Control[] actions)
    {
        int width = 0, height = 0;
        foreach (Control action in actions)
        {
            Size size = action.AutoSize ? action.GetPreferredSize(Size.Empty) : action.Size;
            width += size.Width + action.Margin.Horizontal;
            height = Math.Max(height, size.Height + action.Margin.Vertical);
        }
        return new Size(width, height);
    }
    private void ResizeNavigation()
    {
        int width = Math.Max(1, navigationPanel.ClientSize.Width - navigationPanel.Padding.Horizontal);
        foreach (Button button in navigationPanel.Controls.OfType<Button>()) if (button.Width != width) button.Width = width;
        versionLabel.MaximumSize = new Size(width, 0);
    }
    private static void ConstrainFlow(FlowLayoutPanel flow)
    {
        int width = Math.Max(1, (flow.Parent?.ClientSize.Width ?? flow.Width) - flow.Margin.Horizontal);
        if (flow.MaximumSize.Width != width) flow.MaximumSize = new Size(width, 0);
    }
    private void AdjustComboHeights()
    {
        foreach (DarkComboBox combo in new[] { adapterComboBox, dnsComboBox, activityFilterComboBox })
            combo.ItemHeight = Math.Max(combo.Font.Height + LogicalToDeviceUnits(UiPalette.DropDownItemVerticalPadding), LogicalToDeviceUnits(UiPalette.DropDownItemHeight));
    }
    private void ScaleGridColumns()
    {
        foreach (DataGridView grid in Theme.Descendants(pageHost).OfType<DataGridView>())
        {
            grid.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.DisplayedCells;
            grid.RowTemplate.Height = grid.LogicalToDeviceUnits(32);
            grid.DefaultCellStyle.Padding = new Padding(grid.LogicalToDeviceUnits(6), grid.LogicalToDeviceUnits(4),
                grid.LogicalToDeviceUnits(6), grid.LogicalToDeviceUnits(4));
            foreach (DataGridViewColumn column in grid.Columns)
            {
                if (column.Tag is not Size logical) continue;
                column.MinimumWidth = grid.LogicalToDeviceUnits(logical.Height);
                if (logical.Width > 0) column.Width = grid.LogicalToDeviceUnits(logical.Width);
            }
        }
    }
    private void ResponsiveMenuButton_Click(object? sender, EventArgs e)
    {
        var menu = new ContextMenuStrip { Renderer = new DarkMenuRenderer(), ShowImageMargin = true };
        foreach (string pageName in LayoutPageNames)
        {
            string selectedPage = pageName;
            string title = navigationPanel.Controls[pageName + "NavButton"]?.Text ?? pageName;
            UiIcon icon = (navigationPanel.Controls[pageName + "NavButton"] as RoundedButton)?.Symbol ?? UiIcon.None;
            UiIcons.AddMenuItem(menu.Items, title, icon, (_, _) => SelectLayoutPage(selectedPage));
        }
        menu.Items.Add(new ToolStripSeparator());
        UiIcons.AddMenuItem(menu.Items, "Run as administrator", UiIcon.Shield, (_, args) => ElevateButton_Click(sender, args)).Enabled = !busy && elevateButton.Enabled;
        UiIcons.AddMenuItem(menu.Items, UiLayout.StopText, UiIcon.Stop, (_, args) => { if (busy && stopButton.Enabled) StopButton_Click(sender, args); }).Enabled = busy && stopButton.Enabled;
        menu.Items.Add(new ToolStripSeparator());
        UiIcons.AddMenuItem(menu.Items, "Minimize window", UiIcon.Disable, (_, args) => MinimizeWindowButton_Click(sender, args));
        UiIcons.AddMenuItem(menu.Items, "Maximize / restore window", UiIcon.Apps, (_, args) => MaximizeWindowButton_Click(sender, args));
        menu.Closed += (_, _) => menu.Dispose();
        menu.Show(responsiveMenuButton, new Point(0, responsiveMenuButton.Height));
    }
    internal void SetLayoutCheckSize(Size logical)
    {
        Rectangle available = ResponsiveGeometry.Available(Screen.FromControl(this).WorkingArea, DeviceDpi);
        Size target = new(Math.Min(ResponsiveGeometry.Scale(logical.Width, DeviceDpi), available.Width),
            Math.Min(ResponsiveGeometry.Scale(logical.Height, DeviceDpi), available.Height));
        MinimumSize = new Size(Math.Min(ResponsiveGeometry.Scale(UiLayout.MinimumWidth, DeviceDpi), target.Width),
            Math.Min(ResponsiveGeometry.Scale(UiLayout.MinimumHeight, DeviceDpi), target.Height));
        Size = target; ApplyResponsiveLayout();
    }
}
