using System.Diagnostics;

namespace BoosterX.Core;

public sealed record TempCandidate(string Root, string FullPath, long Length, DateTime LastWriteUtc, DateTime CreatedUtc, string Category = "");
public sealed record ScanProgress(int Visited, int Eligible, string Location);
public sealed record TempScan(IReadOnlyList<TempCandidate> Files, int Skipped, bool Truncated)
{
    public long Bytes => Files.Sum(f => f.Length);
}

public static class TempScanner
{
    public static TempScan Scan(IEnumerable<string> roots, BoosterOptions options, CancellationToken cancellationToken, DateTime? utcNow = null,
        Func<string, bool>? includeFile = null, IProgress<ScanProgress>? progress = null)
    {
        options.Validate();
        DateTime cutoff = (utcNow ?? DateTime.UtcNow).AddDays(-options.TempMinimumAgeDays);
        var files = new List<TempCandidate>();
        var clock = Stopwatch.StartNew();
        int visited = 0, skipped = 0;
        bool truncated = false;
        foreach (string input in roots.Distinct(PathComparer))
        {
            string root = Path.GetFullPath(input).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            if (Path.GetPathRoot(root) == root || !Directory.Exists(root) || HasReparseAncestor(root)) { skipped++; continue; }
            var pending = new Stack<string>();
            pending.Push(root);
            while (pending.Count > 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (clock.Elapsed.TotalSeconds > options.MaxScanSeconds || visited >= options.MaxScannedFiles)
                { truncated = true; break; }
                string directory = pending.Pop();
                try
                {
                    if (HasReparseAncestor(directory)) { skipped++; continue; }
                    foreach (FileSystemInfo item in new DirectoryInfo(directory).EnumerateFileSystemInfos())
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        if (++visited > options.MaxScannedFiles || clock.Elapsed.TotalSeconds > options.MaxScanSeconds)
                        { truncated = true; break; }
                        if (visited % 256 == 0) progress?.Report(new(visited, files.Count, root));
                        try
                        {
                            if ((item.Attributes & FileAttributes.ReparsePoint) != 0) { skipped++; continue; }
                            if (item is DirectoryInfo) { pending.Push(item.FullName); continue; }
                            if (item is FileInfo file && file.LastWriteTimeUtc < cutoff && file.CreationTimeUtc < cutoff
                                && (file.Attributes & (FileAttributes.ReadOnly | FileAttributes.System)) == 0
                                && (includeFile == null || includeFile(file.FullName)))
                                files.Add(new(root, file.FullName, file.Length, file.LastWriteTimeUtc, file.CreationTimeUtc));
                        }
                        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { skipped++; }
                    }
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { skipped++; }
                if (truncated) break;
            }
            if (truncated) break;
        }
        progress?.Report(new(visited, files.Count, "Scan finished"));
        return new(files, skipped, truncated);
    }

    public static bool HasReparseAncestor(string path)
    {
        try
        {
            for (DirectoryInfo? current = new(path); current != null; current = current.Parent)
                if ((current.Attributes & FileAttributes.ReparsePoint) != 0) return true;
            return false;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { return true; }
    }

    public static bool IsInside(string file, string root) => Path.GetFullPath(file).StartsWith(
        Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar,
        OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);

    private static StringComparer PathComparer => OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
}
