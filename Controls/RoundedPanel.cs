namespace BoosterX.App.Controls;

/// <summary>Flat dark surfaces with a soft silhouette and no decorative outline.</summary>
public class RoundedPanel : BufferedPanel
{
    public RoundedPanel() { BackColor = UiPalette.Surface; }
    protected override void OnPaintBackground(PaintEventArgs e) => PaintSurface(this, e.Graphics);
    internal static void PaintSurface(Control control, Graphics graphics)
    {
        using var background = new SolidBrush(control.Parent?.BackColor ?? UiPalette.Background);
        graphics.FillRectangle(background, control.ClientRectangle);
        UiDrawing.FillRound(graphics, control.ClientRectangle, control.LogicalToDeviceUnits(UiPalette.PanelRadius), control.BackColor);
    }
}

public class RoundedNavigationPanel : BufferedFlowLayoutPanel
{
    public RoundedNavigationPanel() { BackColor = UiPalette.Card; }
    protected override void OnPaintBackground(PaintEventArgs e) => RoundedPanel.PaintSurface(this, e.Graphics);
}
