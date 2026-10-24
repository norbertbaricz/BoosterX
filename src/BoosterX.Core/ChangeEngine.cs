using System.Text.Json;

namespace BoosterX.Core;

public enum EntryStatus { Prepared, Applying, Applied, Failed, RolledBack, RecoveryRequired, Restored, RestoreFailed, Skipped }
public sealed record StepOutcome(string Message, bool RestartRequired = false, bool Skipped = false);
public sealed record RunProgress(int Completed, int Total, string Message);

public interface IChangeStep
{
    string Id { get; }
    string Title { get; }
    string Kind { get; }
    bool CanUndo { get; }
    Task<JsonElement> CaptureAsync(CancellationToken cancellationToken);
    Task<StepOutcome> ApplyAsync(JsonElement state, CancellationToken cancellationToken);
}

public interface IUndoService
{
    Task UndoAsync(ChangeEntry entry, CancellationToken cancellationToken);
}

public sealed class ChangeEntry
{
    public string Id { get; set; } = "";
    public string Title { get; set; } = "";
    public string Kind { get; set; } = "";
    public bool CanUndo { get; set; }
    public JsonElement State { get; set; }
    public EntryStatus Status { get; set; } = EntryStatus.Prepared;
    public string Message { get; set; } = "";
    public bool RestartRequired { get; set; }
    public bool NeedsUndo => CanUndo && Status is EntryStatus.Applying or EntryStatus.Applied
        or EntryStatus.RecoveryRequired or EntryStatus.RestoreFailed;
}

public sealed class ChangeRun
{
    public int FormatVersion { get; set; } = 1;
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public DateTimeOffset StartedUtc { get; set; } = DateTimeOffset.UtcNow;
    public long SequenceNumber { get; set; }
    public DateTimeOffset? FinishedUtc { get; set; }
    public string MachineId { get; set; } = "";
    public string UserSid { get; set; } = "";
    public string Title { get; set; } = "";
    public string? RestoreOf { get; set; }
    public List<ChangeEntry> Entries { get; set; } = [];
    public bool WasCancelled { get; set; }
    public bool HasPendingUndo => Entries.Any(e => e.NeedsUndo);
    public bool HasErrors => Entries.Any(e => e.Status is EntryStatus.Failed or EntryStatus.RolledBack
        or EntryStatus.RecoveryRequired or EntryStatus.RestoreFailed);
}

/// <summary>Write-ahead journal: an original value reaches disk before a mutation can start.</summary>
public sealed class RunStore(string directory)
{
    private const long MaxJournalBytes = 16 * 1024 * 1024;
    public string DirectoryPath { get; } = Path.GetFullPath(directory);

    public void Save(ChangeRun run)
    {
        Directory.CreateDirectory(DirectoryPath);
        string target = GetPath(run.Id);
        string temporary = target + ".new";
        byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(run, JsonDefaults.Options);
        if (bytes.LongLength > MaxJournalBytes) throw new InvalidDataException("Journal is too large; no new mutation can start.");
        using (var stream = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None,
                   4096, FileOptions.WriteThrough))
        {
            stream.Write(bytes);
            stream.Flush(flushToDisk: true);
        }
        File.Move(temporary, target, overwrite: true);
    }

    public ChangeRun Load(string id)
    {
        string path = GetPath(id);
        if (new FileInfo(path).Length > MaxJournalBytes) throw new InvalidDataException("Journal is too large.");
        ChangeRun run = JsonSerializer.Deserialize<ChangeRun>(File.ReadAllBytes(path), JsonDefaults.Options)
                        ?? throw new InvalidDataException("Empty journal.");
        if (run.Id != id || run.FormatVersion != 1 || run.Entries == null || run.Entries.Any(entry => entry == null)
            || run.Title == null || run.MachineId == null || run.UserSid == null)
            throw new InvalidDataException("Unsupported, incomplete or mismatched journal.");
        return run;
    }

    public JournalInspection Inspect()
    {
        var runs = new List<ChangeRun>(); var problems = new List<string>();
        if (!Directory.Exists(DirectoryPath)) return new(runs,problems);
        foreach (string path in Directory.EnumerateFiles(DirectoryPath,"*.json"))
        {
            try { runs.Add(Load(Path.GetFileNameWithoutExtension(path))); }
            catch (Exception ex) when (ex is JsonException or InvalidDataException or IOException or UnauthorizedAccessException)
            { problems.Add(Path.GetFileName(path) + ": " + ex.Message); }
        }
        return new(runs.OrderByDescending(run => run.SequenceNumber).ThenByDescending(run => run.StartedUtc).ToArray(),problems);
    }

    public IReadOnlyList<ChangeRun> List()
    {
        if (!Directory.Exists(DirectoryPath)) return [];
        // Do not hide corrupt backups: the caller must report them before allowing newer changes.
        return Directory.EnumerateFiles(DirectoryPath, "*.json")
            .Select(p => Load(Path.GetFileNameWithoutExtension(p)))
            .OrderByDescending(r => r.SequenceNumber).ThenByDescending(r => r.StartedUtc).ToArray();
    }

    private string GetPath(string id)
    {
        if (!Guid.TryParseExact(id, "N", out _)) throw new InvalidDataException("Invalid journal identifier.");
        return Path.Combine(DirectoryPath, id + ".json");
    }
}

public sealed record JournalInspection(IReadOnlyList<ChangeRun> Runs, IReadOnlyList<string> Problems);

public sealed class ChangeEngine(RunStore store, IUndoService undo, string machineId, string userSid)
{
    private readonly SemaphoreSlim gate = new(1, 1);

    public async Task<ChangeRun> RunAsync(string title, IReadOnlyList<IChangeStep> steps,
        IProgress<RunProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            // A crash or a failed rollback must be dealt with before it can be hidden by a newer run.
            IReadOnlyList<ChangeRun> previousRuns = store.List();
            bool interrupted = previousRuns.Any(r => r.MachineId == machineId && r.UserSid == userSid
                && r.Entries.Any(e => e.CanUndo && e.Status is EntryStatus.Applying or EntryStatus.RecoveryRequired or EntryStatus.RestoreFailed));
            if (interrupted) throw new InvalidOperationException("Restore the interrupted/failed run from History before making new changes.");
            if (steps.Select(s => s.Id).Distinct(StringComparer.Ordinal).Count() != steps.Count)
                throw new InvalidOperationException("The plan contains duplicate actions.");

            var run = new ChangeRun { Title = title, MachineId = machineId, UserSid = userSid,
                SequenceNumber = checked(previousRuns.Select(r => r.SequenceNumber).DefaultIfEmpty(0).Max() + 1) };
            store.Save(run);
            for (int i = 0; i < steps.Count; i++)
            {
                if (cancellationToken.IsCancellationRequested) { run.WasCancelled = true; break; }
                IChangeStep step = steps[i];
                progress?.Report(new(i, steps.Count, step.Title));
                var entry = new ChangeEntry { Id = step.Id, Title = step.Title, Kind = step.Kind, CanUndo = step.CanUndo,
                    State = JsonDefaults.Element(new { }) };
                run.Entries.Add(entry);
                try
                {
                    entry.State = await step.CaptureAsync(cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                { entry.Status = EntryStatus.Skipped; entry.Message = "Cancelled before modification."; run.WasCancelled = true; break; }
                catch (Exception ex)
                { entry.Status = EntryStatus.Failed; entry.Message = "Backup/preflight failed; nothing changed. " + ex.Message; store.Save(run); continue; }

                entry.Status = EntryStatus.Applying;
                store.Save(run); // If this fails, ApplyAsync is never called.
                try
                {
                    // Cancellation is observed between steps; do not interrupt a Windows servicing operation.
                    StepOutcome outcome = await step.ApplyAsync(entry.State, CancellationToken.None).ConfigureAwait(false);
                    entry.Status = outcome.Skipped ? EntryStatus.Skipped : EntryStatus.Applied;
                    entry.Message = outcome.Message;
                    entry.RestartRequired = outcome.RestartRequired;
                }
                catch (Exception ex)
                {
                    entry.Status = EntryStatus.Failed;
                    entry.Message = ex.Message;
                    if (step.CanUndo)
                    {
                        try
                        {
                            await undo.UndoAsync(entry, CancellationToken.None).ConfigureAwait(false);
                            entry.Status = EntryStatus.RolledBack;
                            entry.Message += " Original settings restored.";
                        }
                        catch (Exception rollback)
                        { entry.Status = EntryStatus.RecoveryRequired; entry.Message += " Restore failed: " + rollback.Message; }
                    }
                }
                store.Save(run);
                progress?.Report(new(i + 1, steps.Count, entry.Title + ": " + entry.Message));
                if (entry.Status == EntryStatus.RecoveryRequired) break;
            }
            run.FinishedUtc = DateTimeOffset.UtcNow;
            store.Save(run);
            return run;
        }
        finally { gate.Release(); }
    }

    public async Task<ChangeRun> RestoreAsync(string id, IProgress<RunProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            ChangeRun run = store.Load(id);
            if (run.MachineId != machineId || run.UserSid != userSid)
                throw new InvalidOperationException("This backup belongs to another PC or Windows account.");
            ChangeRun? latest = store.List().FirstOrDefault(r => r.MachineId == machineId && r.UserSid == userSid && r.HasPendingUndo);
            if (latest?.Id != run.Id) throw new InvalidOperationException("Restore the latest reversible run first, then work backwards.");
            ChangeEntry[] pending = run.Entries.Where(e => e.NeedsUndo).Reverse().ToArray();
            for (int i = 0; i < pending.Length; i++)
            {
                if (cancellationToken.IsCancellationRequested) break;
                ChangeEntry entry = pending[i];
                progress?.Report(new(i, pending.Length, "Restoring " + entry.Title));
                try
                {
                    await undo.UndoAsync(entry, CancellationToken.None).ConfigureAwait(false);
                    entry.Status = EntryStatus.Restored;
                    entry.Message = "Original settings restored. A sign-out/restart may be required.";
                }
                catch (Exception ex)
                { entry.Status = EntryStatus.RestoreFailed; entry.Message = ex.Message; }
                store.Save(run);
                progress?.Report(new(i + 1, pending.Length, entry.Title + ": " + entry.Message));
                // Preserve reverse dependency order. Retrying must not put a newer snapshot over an older one.
                if (entry.Status == EntryStatus.RestoreFailed) break;
            }
            return run;
        }
        finally { gate.Release(); }
    }
}
