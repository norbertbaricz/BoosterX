using System.Diagnostics;
using System.Text;
using BoosterX.Core;

namespace BoosterX.App.Services;

public sealed class SystemCommand(BoosterOptions options, Action<string> log, Action<double>? progress = null)
{
    private static readonly HashSet<string> Allowed = new(StringComparer.OrdinalIgnoreCase)
        { "dism.exe", "sfc.exe", "netsh.exe", "chkdsk.exe", "ipconfig.exe", "defrag.exe", "powercfg.exe", "w32tm.exe", "wevtutil.exe" };

    public async Task<int> RunAsync(string executable, IReadOnlyList<string> arguments, bool servicing = false)
    {
        if (!Allowed.Contains(executable)) throw new InvalidOperationException("Unsupported Windows command.");
        string fullPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), executable);
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        Encoding encoding = Encoding.GetEncoding((int)NativeMethods.GetOEMCP());
        var start = new ProcessStartInfo(fullPath)
        {
            UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true,
            StandardOutputEncoding = encoding, StandardErrorEncoding = encoding,
            WorkingDirectory = Environment.GetFolderPath(Environment.SpecialFolder.System)
        };
        foreach (string argument in arguments) start.ArgumentList.Add(argument);
        log(executable + " " + string.Join(" ", arguments));
        using var process = new Process { StartInfo = start };
        if (!process.Start()) throw new IOException("Could not start " + executable);
        bool reportsOverallPercent = executable.Equals("dism.exe",StringComparison.OrdinalIgnoreCase) || executable.Equals("sfc.exe",StringComparison.OrdinalIgnoreCase);
        Task output = DrainAsync(process.StandardOutput,reportsOverallPercent);
        Task error = DrainAsync(process.StandardError,reportsOverallPercent);
        // Killing DISM/SFC/CHKDSK mid-operation is intentionally not used as a cancellation mechanism.
        if (servicing)
            await process.WaitForExitAsync().ConfigureAwait(false);
        else
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(options.NativeCommandTimeoutSeconds));
            try { await process.WaitForExitAsync(timeout.Token).ConfigureAwait(false); }
            catch (OperationCanceledException)
            {
                try { process.Kill(entireProcessTree: true); } catch (InvalidOperationException) { }
                await process.WaitForExitAsync().ConfigureAwait(false);
                await Task.WhenAll(output, error).ConfigureAwait(false);
                throw new TimeoutException(executable + " timed out.");
            }
        }
        await Task.WhenAll(output, error).ConfigureAwait(false);
        log(executable + " exit code: " + process.ExitCode);
        return process.ExitCode;
    }

    public static StepOutcome CheckExit(int code, string success, bool outputNeedsReview = false)
    {
        if (code is not (0 or 3010)) throw new IOException($"Windows returned exit code {code}. Review the command output in the log.");
        return new(success + (outputNeedsReview ? " Review the Windows output; completion does not by itself prove that every problem was repaired." : ""), code == 3010);
    }

    private async Task DrainAsync(StreamReader reader,bool reportsOverallPercent)
    {
        char[] buffer = new char[1024];
        var line = new StringBuilder();
        int count;
        while ((count = await reader.ReadAsync(buffer).ConfigureAwait(false)) > 0)
        {
            for (int i = 0; i < count; i++)
            {
                char c = buffer[i];
                if (c == '\0') continue; // SFC output can contain UTF-16 NULs through redirected OEM streams.
                if (c is '\r' or '\n' || line.Length >= 2000)
                { if (line.Length > 0) { Publish(line.ToString(),reportsOverallPercent); line.Clear(); } }
                else line.Append(c);
            }
        }
        if (line.Length > 0) Publish(line.ToString(),reportsOverallPercent);
    }
    private void Publish(string line,bool reportsOverallPercent)
    {
        if (reportsOverallPercent && OperationProgress.ParsePercent(line) is { } value) progress?.Invoke(value);
        log(line);
    }
}
