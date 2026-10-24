using System.Text.Json;

namespace BoosterX.Core;

/// <summary>All easy-to-tune defaults live here. No arbitrary cleanup paths or shell commands.</summary>
public sealed class BoosterOptions
{
    public const string ProductVersion = "4.9.2";
    public const int DefaultTempMinimumAgeDays = 7;       // Protect recently created/modified temporary files.
    public const int DefaultMaxScannedFiles = 100_000;    // Bound a scan on very large temporary trees.
    public const int DefaultMaxScanSeconds = 90;         // Keep scans bounded, including deep empty trees.
    public const int DefaultMenuDelayMilliseconds = 100; // Small UI improvement without force-closing apps.
    public const int DefaultNativeCommandTimeoutSeconds = 120;
    public const int DefaultServiceTimeoutSeconds = 30;
    public const int DefaultPingTimeoutMilliseconds = 1_500;
    public const int DefaultPingSamples = 4;
    public const int DefaultMaxLogCharacters = 160_000;
    public const bool DefaultAutomaticRestorePoint = true;

    public int TempMinimumAgeDays { get; set; } = DefaultTempMinimumAgeDays;
    public int MaxScannedFiles { get; set; } = DefaultMaxScannedFiles;
    public int MaxScanSeconds { get; set; } = DefaultMaxScanSeconds;
    public int MenuDelayMilliseconds { get; set; } = DefaultMenuDelayMilliseconds;
    public int NativeCommandTimeoutSeconds { get; set; } = DefaultNativeCommandTimeoutSeconds;
    public int ServiceTimeoutSeconds { get; set; } = DefaultServiceTimeoutSeconds;
    public int PingTimeoutMilliseconds { get; set; } = DefaultPingTimeoutMilliseconds;
    public int PingSamples { get; set; } = DefaultPingSamples;
    public int MaxLogCharacters { get; set; } = DefaultMaxLogCharacters;
    public bool AutomaticRestorePoint { get; set; } = DefaultAutomaticRestorePoint;

    public void Validate()
    {
        Require(TempMinimumAgeDays is >= 1 and <= 365, nameof(TempMinimumAgeDays));
        Require(MaxScannedFiles is >= 100 and <= 1_000_000, nameof(MaxScannedFiles));
        Require(MaxScanSeconds is >= 5 and <= 600, nameof(MaxScanSeconds));
        Require(MenuDelayMilliseconds is >= 0 and <= 400, nameof(MenuDelayMilliseconds));
        Require(NativeCommandTimeoutSeconds is >= 10 and <= 600, nameof(NativeCommandTimeoutSeconds));
        Require(ServiceTimeoutSeconds is >= 5 and <= 120, nameof(ServiceTimeoutSeconds));
        Require(PingTimeoutMilliseconds is >= 100 and <= 10_000, nameof(PingTimeoutMilliseconds));
        Require(PingSamples is >= 1 and <= 20, nameof(PingSamples));
        Require(MaxLogCharacters is >= 10_000 and <= 2_000_000, nameof(MaxLogCharacters));
    }

    public static BoosterOptions LoadOrCreate(string path)
    {
        BoosterOptions options;
        if (File.Exists(path))
        {
            if (new FileInfo(path).Length > 64_000) throw new InvalidDataException("Settings file is too large.");
            options = JsonSerializer.Deserialize<BoosterOptions>(File.ReadAllText(path), JsonDefaults.Options)
                      ?? throw new InvalidDataException("Settings file is empty.");
        }
        else
        {
            options = new BoosterOptions();
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
            File.WriteAllText(path, JsonSerializer.Serialize(options, JsonDefaults.Options));
        }
        options.Validate();
        return options;
    }

    private static void Require(bool valid, string name)
    {
        if (!valid) throw new InvalidDataException($"Invalid setting: {name}. See BoosterOptions.cs for allowed limits.");
    }
}

public static class JsonDefaults
{
    public static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
    };

    public static JsonElement Element<T>(T value) => JsonSerializer.SerializeToElement(value, Options);
    public static T Read<T>(JsonElement element) => element.Deserialize<T>(Options)
        ?? throw new InvalidDataException($"Missing {typeof(T).Name} snapshot.");
}
