using System.ComponentModel;
using System.Diagnostics;
using System.Security.Principal;

namespace BoosterX.App.Services;

internal static class ElevationService
{
    public static bool IsAdministrator
    {
        get
        {
            using WindowsIdentity identity = WindowsIdentity.GetCurrent();
            return new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
        }
    }

    public static bool TryRestart()
    {
        string executable = Environment.ProcessPath ?? throw new IOException("The running executable could not be located.");
        var start = new ProcessStartInfo(executable)
        {
            UseShellExecute = true,
            Verb = "runas",
            WorkingDirectory = AppContext.BaseDirectory
        };
        if (Path.GetFileNameWithoutExtension(executable).Equals("dotnet", StringComparison.OrdinalIgnoreCase))
        {
            string assembly = Path.Combine(AppContext.BaseDirectory, "BoosterX.dll");
            if (!File.Exists(assembly)) throw new FileNotFoundException("The application assembly was not found.", assembly);
            start.ArgumentList.Add(assembly);
        }
        start.ArgumentList.Add("--wait-for-pid=" + Environment.ProcessId);
        try
        {
            using Process? process = Process.Start(start);
            return process != null;
        }
        catch (Win32Exception ex) when (ex.NativeErrorCode == 1223) { return false; }
    }
}
