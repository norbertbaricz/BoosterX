using System.Runtime.InteropServices;
using System.Text;
using BoosterX.Core;

namespace BoosterX.App.Services;

public sealed record CleanupResult(int Deleted, long Bytes, int Skipped);

public sealed class CleanupService(BoosterOptions options)
{
    public static IReadOnlyList<string> AllowedRoots => CleanupCatalog.Locations().Select(location => location.Root).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();

    public TempScan Scan(CancellationToken cancellationToken,
        IProgress<ScanProgress>? progress = null, Action<string>? log = null)
    {
        var allowed = new List<CleanupLocation>(); int blocked = 0;
        foreach (IGrouping<string,CleanupLocation> group in CleanupCatalog.Locations().GroupBy(l => l.Category.Id))
        {
            string? reason = CleanupCatalog.BlockReason(group.First().Category);
            if (reason != null) { blocked++; log?.Invoke(group.First().Category.Title + ": " + reason); }
            else allowed.AddRange(group);
        }
        TempScan scan = TempScanner.Scan(allowed.Select(l => l.Root),options,cancellationToken,
            includeFile:path => allowed.Any(location => CleanupCatalog.Matches(location,path)),progress:progress);
        TempCandidate[] files = scan.Files.Select(file => file with { Category = allowed.First(l => l.Root.Equals(file.Root,StringComparison.OrdinalIgnoreCase) && CleanupCatalog.Matches(l,file.FullPath)).Category.Title }).ToArray();
        return new(files,scan.Skipped+blocked,scan.Truncated);
    }

    public CleanupResult Delete(IReadOnlyList<TempCandidate> candidates, Action<string> log, Action<int,int>? progress = null)
    {
        int deleted = 0, skipped = 0;
        long bytes = 0;
        DateTime cutoff = DateTime.UtcNow.AddDays(-options.TempMinimumAgeDays);
        var candidateRoots = candidates.Select(c => Path.GetFullPath(c.Root)).ToHashSet(StringComparer.OrdinalIgnoreCase);
        IReadOnlyList<CleanupLocation> locations = CleanupCatalog.Locations().Where(location => candidateRoots.Contains(location.Root)).ToArray();
        var blocked = new HashSet<string>(); var clock = System.Diagnostics.Stopwatch.StartNew(); long checkedAt = -CleanupCatalog.IdleRecheckMilliseconds;
        for (int index = 0; index < candidates.Count; index++)
        {
            TempCandidate candidate = candidates[index];
            if (index % 32 == 0) progress?.Invoke(index,candidates.Count);
            try
            {
                if (clock.ElapsedMilliseconds - checkedAt >= CleanupCatalog.IdleRecheckMilliseconds)
                {
                    blocked = locations.Select(l => l.Category).DistinctBy(c => c.Id)
                        .Where(c => CleanupCatalog.BlockReason(c) != null).Select(c => c.Id).ToHashSet(StringComparer.Ordinal);
                    checkedAt = clock.ElapsedMilliseconds;
                }
                string root = Path.GetFullPath(candidate.Root);
                if (!locations.Any(location => location.Root.Equals(root,StringComparison.OrdinalIgnoreCase)
                        && !blocked.Contains(location.Category.Id) && CleanupCatalog.Matches(location,candidate.FullPath))
                    || !TempScanner.IsInside(candidate.FullPath, root) || TempScanner.HasReparseAncestor(root)
                    || TempScanner.HasReparseAncestor(Path.GetDirectoryName(candidate.FullPath)!)) { skipped++; continue; }

                // Delete by an exclusive, non-following handle; never truncate or force-unlock a file.
                using var handle = NativeMethods.CreateFile(candidate.FullPath, 0x10000 | 0x80, 0,
                    IntPtr.Zero, 3, 0x00200000, IntPtr.Zero);
                if (handle.IsInvalid || !NativeMethods.GetFileInformationByHandle(handle, out var info)) { skipped++; continue; }
                if ((info.Attributes & (uint)(FileAttributes.ReparsePoint | FileAttributes.Directory | FileAttributes.ReadOnly | FileAttributes.System)) != 0)
                { skipped++; continue; }
                var finalPath = new StringBuilder(32768);
                uint length = NativeMethods.GetFinalPathNameByHandle(handle, finalPath, (uint)finalPath.Capacity, 0);
                if (length == 0 || length >= finalPath.Capacity) { skipped++; continue; }
                string resolved = finalPath.ToString();
                if (resolved.StartsWith(@"\\?\UNC\", StringComparison.OrdinalIgnoreCase)) resolved = @"\\" + resolved[8..];
                else if (resolved.StartsWith(@"\\?\", StringComparison.Ordinal)) resolved = resolved[4..];
                long size = ((long)info.SizeHigh << 32) | info.SizeLow;
                DateTime written = FileTime(info.Written), created = FileTime(info.Created);
                if (!TempScanner.IsInside(resolved, root) || size != candidate.Length
                    || written != candidate.LastWriteUtc || created != candidate.CreatedUtc || written >= cutoff || created >= cutoff)
                { skipped++; continue; }
                var disposition = new NativeMethods.FileDisposition { DeleteFile = 1 };
                if (!NativeMethods.SetFileInformationByHandle(handle, 4, ref disposition, 1)) { skipped++; continue; }
                bytes += size;
                deleted++;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
            { skipped++; }
        }
        progress?.Invoke(candidates.Count,candidates.Count);
        log($"Temporary cleanup: {deleted:N0} files deleted, {FormatBytes(bytes)} logical file bytes removed, {skipped:N0} skipped.");
        return new(deleted, bytes, skipped);
    }

    public static (long Items, long Bytes) GetRecycleBin()
    {
        var info = new NativeMethods.RecycleInfo { Size = (uint)Marshal.SizeOf<NativeMethods.RecycleInfo>() };
        int code = NativeMethods.SHQueryRecycleBin(null, ref info);
        Marshal.ThrowExceptionForHR(code);
        return (info.Items, info.Bytes);
    }

    public static void EmptyRecycleBin()
    {
        int code = NativeMethods.SHEmptyRecycleBin(IntPtr.Zero, null, 1 | 2 | 4);
        Marshal.ThrowExceptionForHR(code);
    }

    public static string FormatBytes(long bytes) => bytes >= 1024L * 1024 * 1024
        ? $"{bytes / (1024d * 1024 * 1024):0.00} GB" : bytes >= 1024 * 1024
        ? $"{bytes / (1024d * 1024):0.0} MB" : $"{bytes / 1024d:0.0} KB";

    private static DateTime FileTime(System.Runtime.InteropServices.ComTypes.FILETIME time) =>
        DateTime.FromFileTimeUtc(((long)time.dwHighDateTime << 32) | (uint)time.dwLowDateTime);
}
