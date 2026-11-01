using BoosterX.App.Controls;
using BoosterX.Core;

namespace BoosterX.App;

public partial class Form1
{
    private const string TrayOpenText = "Open BoosterX";
    private const string TrayActivityText = "Open Activity";
    private const string TrayQuitText = "Quit";
    private const string TrayQuittingText = "Quitting after current action…";
    private bool hiddenByTray, hidePosted, exitRequested, allowClose, quitWhenIdle, exitPosted, trayFixtureMode;
    private FormWindowState trayRestoreState = FormWindowState.Normal;
    private ToolStripMenuItem? trayQuitItem;

    private void InitializeDesktopNotifications()
    {
        desktopMenu = new ContextMenuStrip { Renderer = new DarkMenuRenderer(),BackColor = UiPalette.Surface,ForeColor = UiPalette.Text,ShowImageMargin = true };
        UiIcons.AddMenuItem(desktopMenu.Items,TrayOpenText,UiIcon.Dashboard,(_,_) => RestoreFromTray());
        UiIcons.AddMenuItem(desktopMenu.Items,TrayActivityText,UiIcon.Activity,(_,_) =>
        {
            if (resourcesClosed) return;
            RestoreFromTray(); ShowPage(activityPage,activityNavButton);
        });
        desktopMenu.Items.Add(new ToolStripSeparator());
        trayQuitItem = UiIcons.AddMenuItem(desktopMenu.Items,TrayQuitText,UiIcon.Error,(_,_) =>
        {
            // Let the menu finish its click/close dispatch before disposing any tray resources.
            if (!resourcesClosed && !IsDisposed)
            {
                _ = Handle; BeginInvoke((Action)RequestQuit);
            }
        });
        trayQuitItem.Name = "trayQuitItem";
        desktopIcon = new NotifyIcon { Icon = ownedIcon,Text = "BoosterX",ContextMenuStrip = desktopMenu,Visible = true };
        desktopIcon.MouseClick += (_,args) => { if (args.Button == MouseButtons.Left) RestoreFromTray(); };
        desktopIcon.BalloonTipClicked += (_,_) =>
        {
            if (resourcesClosed) return;
            RestoreFromTray(); ShowPage(activityPage,activityNavButton);
        };
        help.SetToolTip(notificationsCheckBox,"Controls pop-up notifications. The notification-area icon remains available.");
    }
    private void HideToTray()
    {
        if (resourcesClosed || desktopIcon == null) return;
        desktopIcon.Visible = true;
        if (WindowState != FormWindowState.Minimized) trayRestoreState = WindowState;
        hiddenByTray = true;
        Hide(); ShowInTaskbar = false;
    }
    private void QueueHideToTray()
    {
        if (hidePosted || resourcesClosed || !IsHandleCreated) return;
        hidePosted = true;
        BeginInvoke((Action)(() =>
        {
            hidePosted = false;
            if (!resourcesClosed && !allowClose) HideToTray();
        }));
    }
    private void RestoreFromTray()
    {
        if (resourcesClosed || IsDisposed) return;
        ShowInTaskbar = true;
        WindowState = trayRestoreState;
        Show(); hiddenByTray = false;
        FitToWorkingArea(initial:false); ApplyResponsiveLayout();
        FlushLog(); RefreshActivityView();
        BringToFront(); Activate();
    }
    private void RequestQuit()
    {
        if (resourcesClosed || IsDisposed || quitWhenIdle) return;
        exitRequested = true;
        if (!busy) { CloseForExit(); return; }
        quitWhenIdle = true;
        cancellation?.Cancel(); stopButton.Enabled = false;
        if (trayQuitItem != null) { trayQuitItem.Text = TrayQuittingText; trayQuitItem.Enabled = false; }
        if (desktopIcon != null) desktopIcon.Text = "BoosterX - Quitting after current action";
        SetStatus("Quitting after the current action finishes. Remaining actions will be skipped.");
        WriteActivity("Quit requested. Waiting for the current action; remaining actions will be skipped.",Services.ActivityLevel.Info);
    }
    private void CloseForExit()
    {
        if (resourcesClosed || IsDisposed) return;
        if (busy) { RequestQuit(); return; }
        // A queued Quit blocks new work; only this idle exit path permits actual window disposal.
        allowClose = true; exitRequested = true; quitWhenIdle = false;
        Close();
    }
    private void TryCloseAfterOperation()
    {
        if (!quitWhenIdle || busy || exitPosted || resourcesClosed || IsDisposed) return;
        exitPosted = true;
        _ = Handle;
        BeginInvoke((Action)(() =>
        {
            exitPosted = false;
            if (!resourcesClosed && !busy) CloseForExit();
        }));
    }
    private void ShowErrorOrNotify(string message,string title)
    {
        if (hiddenByTray || quitWhenIdle) NotifyDesktop(title,message,ToolTipIcon.Error);
        else DarkDialog.Show(this,message,title,MessageBoxButtons.OK,MessageBoxIcon.Error);
    }
    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        base.OnFormClosing(e);
        if (e.Cancel) return;
        bool trayReady = (runtimeReady || trayFixtureMode) && desktopIcon?.Icon != null && !resourcesClosed;
        TrayCloseDecision decision = TrayClosePolicy.Decide(e.CloseReason == CloseReason.UserClosing,allowClose,busy,trayReady);
        if (decision == TrayCloseDecision.Hide) { e.Cancel = true; QueueHideToTray(); }
        else if (decision == TrayCloseDecision.WaitForCurrentAction) { e.Cancel = true; RequestQuit(); }
        // OS shutdown/application exit and explicit idle Quit proceed to normal resource disposal.
    }
    internal static IReadOnlyList<string> VerifyTrayLifecycleFixtures()
    {
        var issues = new List<string>();
        using var fixture = new Form1(layoutCheckOnly:true) { Text = "BoosterX tray fixture - no Windows changes" };
        fixture.trayFixtureMode = true;
        fixture.Show(); Application.DoEvents();
        fixture.InitializeDesktopNotifications(); fixture.desktopIcon!.Text = "BoosterX tray self-check";
        fixture.uiPreferences.DesktopNotifications = false;
        fixture.WindowState = FormWindowState.Maximized; Application.DoEvents();
        fixture.Close(); Application.DoEvents();
        if (fixture.IsDisposed || fixture.Visible || fixture.ShowInTaskbar || !fixture.desktopIcon.Visible)
            issues.Add("Window X does not hide to a persistent tray icon.");
        fixture.RestoreFromTray(); Application.DoEvents();
        if (!fixture.Visible || !fixture.ShowInTaskbar || fixture.IsDisposed || fixture.WindowState != FormWindowState.Maximized)
            issues.Add("The tray icon cannot restore the same maximized window.");
        fixture.busy = true; fixture.cancellation = new CancellationTokenSource();
        fixture.Close(); Application.DoEvents();
        if (fixture.IsDisposed || fixture.Visible || fixture.cancellation.IsCancellationRequested)
            issues.Add("Window X interrupts active work instead of hiding it.");
        fixture.trayQuitItem!.PerformClick(); Application.DoEvents();
        if (fixture.IsDisposed || !fixture.quitWhenIdle || !fixture.cancellation.IsCancellationRequested)
            issues.Add("Busy tray Quit does not wait cooperatively for the current action.");
        fixture.busy = false; fixture.TryCloseAfterOperation(); Application.DoEvents();
        if (!fixture.IsDisposed || !fixture.resourcesClosed || fixture.desktopIcon.Visible)
            issues.Add("Tray Quit does not dispose the window/icon after work finishes.");
        return issues;
    }
}
