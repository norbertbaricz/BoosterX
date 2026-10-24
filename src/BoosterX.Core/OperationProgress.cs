using System.Globalization;
using System.Text.RegularExpressions;

namespace BoosterX.Core;

/// <summary>Monotonic work progress; unknown-duration operations never receive an invented percentage.</summary>
public sealed class OperationProgress
{
    private int completed, total;
    private double stepFraction, displayed;
    public double? Percent => total > 0 ? displayed : null;
    public bool IsIndeterminate => total == 0 || completed < total && stepFraction == 0;
    public string Caption => Percent is { } value ? $"{Math.Clamp((int)Math.Floor(value), 0, 100)}%" : "Working…";
    public void Reset() { completed = total = 0; stepFraction = displayed = 0; }
    public void SetSteps(int done, int count)
    {
        count = Math.Max(1, count); done = Math.Clamp(done, 0, count);
        if (count != total || done != completed) stepFraction = 0;
        total = count; completed = done; Update();
    }
    public void SetStepPercent(double percent)
    {
        if (!double.IsFinite(percent)) return;
        if (total == 0) { total = 1; completed = 0; }
        stepFraction = Math.Max(stepFraction, Math.Clamp(percent / 100, 0, 0.99)); Update();
    }
    public void Complete() { total = 1; completed = 1; stepFraction = 0; displayed = 100; }
    private void Update() => displayed = Math.Max(displayed, Math.Min(100, 100d * (completed + (completed < total ? stepFraction : 0)) / total));
    public static double? ParsePercent(string text)
    {
        Match match = Regex.Match(text, @"(?<![-\d.,])(?<p>\d{1,3}(?:[.,]\d+)?)\s*%", RegexOptions.CultureInvariant);
        return match.Success && double.TryParse(match.Groups["p"].Value.Replace(',', '.'), NumberStyles.AllowDecimalPoint,
            CultureInfo.InvariantCulture, out double value) && value is >= 0 and <= 100 ? value : null;
    }
}
