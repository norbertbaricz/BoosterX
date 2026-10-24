namespace BoosterX.Core;

/// <summary>Read-only idle checks, independent of the service-change allowlist.</summary>
public static class CleanupServiceGate
{
    public static string? BlockReason(IEnumerable<string> names, Func<string,uint> readState, Func<bool> restartPending)
    {
        string[] services = names.ToArray();
        if (services.Any(name => name is not ("wuauserv" or "BITS")))
            throw new InvalidDataException("Unsupported cleanup service check.");
        if (services.Length == 0) return null;
        try
        {
            // Only SERVICE_STOPPED is idle. Pending/paused states also exclude this cache.
            foreach (string name in services)
                if (readState(name) != 1) return name + " is active or transitioning; update cache is excluded.";
            return restartPending() ? "A Windows restart is pending; update cache is excluded." : null;
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or IOException
            or UnauthorizedAccessException or System.Security.SecurityException)
        { return "Update service or restart state could not be verified; update cache is excluded."; }
    }
}
