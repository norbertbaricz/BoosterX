namespace BoosterX.App.Services;

internal enum ActivityLevel { Info, Success, Warning, Error }

internal sealed record ActivityEntry(long Id, DateTimeOffset At, ActivityLevel Level, string Details)
{
    public string Time => At.ToLocalTime().ToString("HH:mm:ss");
    public string Status => Level.ToString();
    public string Message
    {
        get
        {
            string value = Details.Split(['\r', '\n'], 2, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? "";
            return value.Length > 350 ? value[..347] + "…" : value;
        }
    }
}
