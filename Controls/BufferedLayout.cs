namespace BoosterX.App.Controls;

// Buffer container backgrounds locally; do not impose WS_EX_COMPOSITED on native child windows.
public class BufferedPanel : Panel
{
    public BufferedPanel() { DoubleBuffered = true; ResizeRedraw = true; }
}
public class BufferedTableLayoutPanel : TableLayoutPanel
{
    public BufferedTableLayoutPanel() { DoubleBuffered = true; ResizeRedraw = true; }
}
public class BufferedFlowLayoutPanel : FlowLayoutPanel
{
    public BufferedFlowLayoutPanel() { DoubleBuffered = true; ResizeRedraw = true; }
}
