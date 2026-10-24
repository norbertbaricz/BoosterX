using System.Text.Json;
using BoosterX.App.Services;

namespace BoosterX.RenderingChecks;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        ApplicationConfiguration.Initialize();
        string output = Path.GetFullPath(args.FirstOrDefault() ?? "RenderingChecks");
        Directory.CreateDirectory(output);
        using var font = new Font("Segoe UI", 10);
        IReadOnlyList<RenderingCheckResult> results = ControlRenderingChecks.Run(font, output).Concat(InteractionChecks.Run(font, output)).ToArray();
        foreach (RenderingCheckResult check in results)
            Console.WriteLine((check.Passed ? "PASS " : "FAIL ") + check.Name + ": " + check.Details);
        int passed = results.Count(r => r.Passed);
        Console.WriteLine($"{passed}/{results.Count} rendering checks passed.");
        File.WriteAllText(Path.Combine(output, "checks.json"), JsonSerializer.Serialize(new
        {
            Framework = Environment.Version.ToString(), Renderer = "Actual WinForms controls; foreground repaint, dropdown interaction and scrolling",
            DesktopCaptured = false, Passed = passed, Total = results.Count, Results = results
        }, new JsonSerializerOptions { WriteIndented = true }));
        return passed == results.Count ? 0 : 1;
    }
}
