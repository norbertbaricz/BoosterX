using BoosterX.App.Controls;

namespace BoosterX.App;

public partial class Form1
{
    private readonly RoundedPanel footerProgressPanel = new()
    {
        Name = "footerProgressPanel",Tag = "card",Margin = Padding.Empty,Anchor = AnchorStyles.Left | AnchorStyles.Right,
        Size = new(240,UiPalette.FieldHeight),TabStop = false,AccessibleName = "Operation progress"
    };
    private string footerStatus = "Ready. Analyze your PC or review a plan.";
    private bool footerFixtureActive;
    private bool FooterProgressActive => busy || layoutCheckOnly && footerFixtureActive;

    private void InitializeFooterControls()
    {
        // Progress takes all remaining row width; Stop reserves only its measured natural width.
        footerLayout.ColumnCount = 3; footerLayout.ColumnStyles.Clear();
        footerLayout.ColumnStyles.Add(new(SizeType.Absolute,0));
        footerLayout.ColumnStyles.Add(new(SizeType.Percent,100)); footerLayout.ColumnStyles.Add(new(SizeType.Absolute,0));
        footerLayout.RowCount = 2; footerLayout.RowStyles.Clear();
        footerLayout.RowStyles.Add(new(SizeType.Absolute,UiPalette.FieldHeight)); footerLayout.RowStyles.Add(new(SizeType.Absolute,0));
        operationProgress.Dock = DockStyle.None; operationProgress.Anchor = AnchorStyles.Top | AnchorStyles.Left;
        operationProgress.AccessibleName = "Operation completion";
        operationPercentLabel.UseMnemonic = false; operationPercentLabel.AccessibleName = "Completion percentage";
        footerProgressPanel.Controls.Add(operationProgress); footerProgressPanel.Controls.Add(operationPercentLabel);
        footerLayout.Controls.Add(footerProgressPanel,1,0); footerLayout.SetCellPosition(stopButton,new(2,0));
        stopButton.Margin = Padding.Empty; stopButton.Symbol = UiIcon.Stop; stopButton.AccessibleName = UiLayout.StopText;
        help.SetToolTip(stopButton,"Stop after the current action finishes. Windows servicing is allowed to finish.");
        operationPercentLabel.FontChanged += (_,_) => QueueResponsiveLayout();
        stopButton.FontChanged += (_,_) => QueueResponsiveLayout();
        UpdateFooterStatus(footerStatus); RefreshProgressDisplay();
        LayoutFooter(Math.Max(1,ClientSize.Width-Padding.Horizontal),true);
    }
    private int StopCaptionWidth(string caption)
    {
        Size text = UiDrawing.MeasureButtonText(caption,stopButton.Font,false);
        if (stopButton.Symbol != UiIcon.None) text = IconGeometry.Measure(text,LogicalToDeviceUnits(UiPalette.ActionIconSize),LogicalToDeviceUnits(UiPalette.ActionIconGap));
        return text.Width+stopButton.Padding.Horizontal+LogicalToDeviceUnits(UiPalette.ButtonTextSafety);
    }
    private void LayoutFooter(int availableWidth,bool fullCaption)
    {
        int gap = LogicalToDeviceUnits(UiPalette.ActionGap);
        int inset = LogicalToDeviceUnits(UiPalette.FooterProgressInset);
        int innerGap = LogicalToDeviceUnits(UiPalette.FooterProgressInnerGap);
        int captionWidth = Math.Max(LogicalToDeviceUnits(UiPalette.FooterCaptionMinimumWidth),
            TextRenderer.MeasureText("100%",operationPercentLabel.Font,Size.Empty,TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix).Width
            +LogicalToDeviceUnits(UiPalette.ButtonTextSafety));
        string stopText = fullCaption ? UiLayout.StopText : UiLayout.CompactStopText;
        if (StopCaptionWidth(stopText) > availableWidth) stopText = "Stop";
        if (stopButton.Text != stopText) { stopButton.MinimumSize = Size.Empty; stopButton.Text = stopText; }
        stopButton.Visible = true;
        Size stop = stopButton.GetPreferredSize(Size.Empty);
        int height = Math.Max(LogicalToDeviceUnits(UiPalette.FieldHeight),Math.Max(stop.Height,operationPercentLabel.Font.Height+inset));
        int overhead = inset*2+innerGap+captionWidth;
        int minimumWidth = overhead+LogicalToDeviceUnits(UiPalette.FooterProgressTrackMinimumWidth);
        bool stacked = minimumWidth+gap+stop.Width > availableWidth;
        int width = Math.Max(overhead+1,availableWidth-(stacked ? 0 : gap+stop.Width));
        footerLayout.ColumnStyles[2].Width = stacked ? 0 : stop.Width+gap;
        footerLayout.SetCellPosition(footerProgressPanel,new(1,0));
        footerLayout.SetCellPosition(stopButton,new(stacked ? 1 : 2,stacked ? 1 : 0));
        footerLayout.RowStyles[0].Height = height;
        footerLayout.RowStyles[1].Height = stacked ? stop.Height+LogicalToDeviceUnits(UiPalette.FooterStackGap) : 0;
        stopButton.Margin = stacked ? new(0,LogicalToDeviceUnits(UiPalette.FooterStackGap),0,0) : new(gap,0,0,0);
        footerProgressPanel.Size = new(width,height);
        int trackHeight = LogicalToDeviceUnits(UiPalette.ProgressHeight);
        operationProgress.Bounds = new(inset,(height-trackHeight)/2,Math.Max(1,width-overhead),trackHeight);
        operationPercentLabel.Bounds = new(width-inset-captionWidth,0,captionWidth,height);
    }
    private void UpdateFooterStatus(string message)
    {
        footerStatus = message;
        footerProgressPanel.AccessibleDescription = message; operationProgress.AccessibleDescription = message;
        operationPercentLabel.AccessibleDescription = message;
        help.SetToolTip(footerProgressPanel,message); help.SetToolTip(operationProgress,message); help.SetToolTip(operationPercentLabel,message);
    }
    internal void SetFooterLayoutCheck(int phase)
    {
        if (!layoutCheckOnly) throw new InvalidOperationException("Footer fixtures require layout-check mode.");
        footerFixtureActive = phase != 0; workProgress.Reset();
        if (phase == 2) { workProgress.SetSteps(1,4); workProgress.SetStepPercent(50); }
        else if (phase == 3) workProgress.Complete();
        RefreshProgressDisplay();
    }
    private IEnumerable<string> VerifyFooterStateFixtures()
    {
        for (int phase = 0; phase < 4; phase++)
        {
            SetFooterLayoutCheck(phase);
            string expected = phase == 2 ? "37%" : phase == 3 ? "100%" : "0%";
            if (operationPercentLabel.Text != expected) yield return "Footer percentage does not match its operation state.";
            int expectedValue = phase == 2 ? 375 : phase == 3 ? 1000 : 0;
            if (operationProgress.Value != expectedValue) yield return "Footer fill does not match reported work progress.";
            if (operationProgress.Style != (phase == 1 ? ProgressBarStyle.Marquee : ProgressBarStyle.Continuous))
                yield return "Footer animation does not match its operation state.";
            if (phase == 0 && (operationProgress.Value != 0 || operationProgress.Style != ProgressBarStyle.Continuous))
                yield return "Idle footer is not empty at 0%.";
        }
        SetFooterLayoutCheck(0);
        if (operationProgress.Value != 0 || operationPercentLabel.Text != "0%") yield return "A completed footer cannot reset to idle.";
    }
    private IEnumerable<string> VerifyFooterLayout()
    {
        if (Theme.Descendants(footerLayout).Any(control => control.Name == "statusLabel")) yield return "The old footer status message is still present.";
        if (!ReferenceEquals(operationProgress.Parent,footerProgressPanel)) yield return "Progress is outside its rounded panel.";
        if (!footerProgressPanel.ClientRectangle.Contains(operationProgress.Bounds) || !footerProgressPanel.ClientRectangle.Contains(operationPercentLabel.Bounds))
            yield return "Progress is clipped inside its panel.";
        if (operationProgress.Bounds.IntersectsWith(operationPercentLabel.Bounds)) yield return "The progress track overlaps its percentage.";
        int textWidth = TextRenderer.MeasureText(operationPercentLabel.Text,operationPercentLabel.Font,Size.Empty,TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix).Width;
        if (textWidth > operationPercentLabel.Width) yield return "The footer clips its progress caption.";
        if (operationProgress.Left != LogicalToDeviceUnits(UiPalette.FooterProgressInset)
            || footerProgressPanel.Width-operationPercentLabel.Right != LogicalToDeviceUnits(UiPalette.FooterProgressInset))
            yield return "Progress panel outer insets are unequal.";
        if (operationProgress.Right+LogicalToDeviceUnits(UiPalette.FooterProgressInnerGap) != operationPercentLabel.Left) yield return "The progress gap is inconsistent.";
        if (Math.Abs(operationProgress.Top*2+operationProgress.Height-footerProgressPanel.Height) > 1) yield return "The progress track is not vertically centered.";
        if (footerProgressPanel.Left != 0 || stopButton.Right != footerLayout.ClientSize.Width) yield return "The footer does not align to both outer edges.";
        if (footerLayout.GetRow(footerProgressPanel) == footerLayout.GetRow(stopButton))
        {
            if (footerProgressPanel.Right+LogicalToDeviceUnits(UiPalette.ActionGap) != stopButton.Left) yield return "Progress does not fill the space beside Stop.";
            if (Math.Abs(footerProgressPanel.Top*2+footerProgressPanel.Height-stopButton.Top*2-stopButton.Height) > 1) yield return "Progress and Stop do not share a vertical center.";
        }
        else if (footerProgressPanel.Right != footerLayout.ClientSize.Width) yield return "Stacked progress does not fill its row.";
        if (!FooterProgressActive && (operationProgress.Value != 0 || operationPercentLabel.Text != "0%" || operationProgress.Style != ProgressBarStyle.Continuous))
            yield return "Inactive progress retains a previous operation's state.";
        if (!versionLabel.Text.Contains(UiLayout.AttributionText,StringComparison.Ordinal)) yield return "Sidebar attribution is missing.";
        if (!stopButton.Visible) yield return "The footer hides the Stop action.";
    }
}
