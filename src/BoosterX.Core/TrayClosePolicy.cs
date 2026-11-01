namespace BoosterX.Core;

public enum TrayCloseDecision { Hide, WaitForCurrentAction, Close }

/// <summary>Window-close intent is separate from a real application quit and OS shutdown.</summary>
public static class TrayClosePolicy
{
    public static TrayCloseDecision Decide(bool userClose,bool explicitQuit,bool busy,bool trayReady)
    {
        if (!userClose) return TrayCloseDecision.Close;
        if (!explicitQuit && trayReady) return TrayCloseDecision.Hide;
        return busy ? TrayCloseDecision.WaitForCurrentAction : TrayCloseDecision.Close;
    }
}
