using BoosterX.App.Controls;
using System.Diagnostics;
using BoosterX.App.Services;
using BoosterX.Core;

namespace BoosterX.App;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        ApplicationConfiguration.Initialize();
        if (!OperatingSystem.IsWindowsVersionAtLeast(10, 0, 19041))
        {
            DarkDialog.Show("BoosterX requires Windows 10 build 19041 or later, or Windows 11.", "BoosterX",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return 1;
        }
        try
        {
            WaitForPreviousInstance(args);
            using var mutex = new Mutex(true, @"Global\BoosterX-Optimizer-v4", out bool created);
            if (!created)
            {
                DarkDialog.Show("BoosterX is already running on this PC.", "BoosterX");
                return 1;
            }
            try
            {
                using var com = new ComSecurityScope();
                Theme.InitializeNativeColors(); // Configure COM callback security before any optional native theme services.
                string data = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "BoosterX");
                Directory.CreateDirectory(data);
                BoosterOptions options = BoosterOptions.LoadOrCreate(Path.Combine(data, "settings.json"));
                if (args.Contains("--ui-check", StringComparer.Ordinal))
                    return UiSelfCheck.Run(data);
                if (args.Contains("--self-check", StringComparer.Ordinal))
                    return WindowsSelfCheck.RunAsync(options, data).GetAwaiter().GetResult();
                using var form = new Form1(options,data);
                System.Threading.ThreadExceptionEventHandler handler = (_,eventArgs) => form.ReportUnexpectedUiException(eventArgs.Exception);
                Application.ThreadException += handler;
                try { Application.Run(form); }
                finally { Application.ThreadException -= handler; }
                return 0;
            }
            finally { mutex.ReleaseMutex(); }
        }
        catch (Exception ex)
        {
            if (args.Any(argument => argument is "--ui-check" or "--self-check"))
            {
                Console.Error.WriteLine(ex);
                string directory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"BoosterX","CheckFailure");
                Directory.CreateDirectory(directory); File.WriteAllText(Path.Combine(directory,"latest.txt"),ex.ToString());
                return 1;
            }
            DarkDialog.Show("BoosterX could not start.\n\n" + ex.Message
                + "\n\nSettings: %LOCALAPPDATA%\\BoosterX\\settings.json", "BoosterX",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
            return 1;
        }
    }

    private static void WaitForPreviousInstance(IEnumerable<string> args)
    {
        const string prefix = "--wait-for-pid=";
        string? argument = args.FirstOrDefault(a => a.StartsWith(prefix, StringComparison.Ordinal));
        if (argument == null || !int.TryParse(argument[prefix.Length..], out int id) || id == Environment.ProcessId) return;
        try
        {
            using Process previous = Process.GetProcessById(id);
            if (!previous.WaitForExit(15_000)) throw new IOException("The previous BoosterX instance is still closing. Retry after it exits.");
        }
        catch (ArgumentException) { } // The previous process has already exited.
    }
}
