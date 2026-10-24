using System.ComponentModel;
using System.Runtime.CompilerServices;
using BoosterX.App.Services;
using BoosterX.Core;

namespace BoosterX.App;

internal interface ISelectableRow { bool Selected { get; set; } bool CanSelect { get; } }

internal abstract class SelectionRow<T>(T item, bool selected = false, bool canSelect = true)
    : INotifyPropertyChanged, ISelectableRow
{
    private bool selected = selected && canSelect;
    public T Item { get; } = item;
    public bool CanSelect { get; } = canSelect;
    public bool Selected
    {
        get => selected;
        set
        {
            if (!CanSelect || selected == value) return;
            selected = value;
            Notify(nameof(Selected));
        }
    }
    public event PropertyChangedEventHandler? PropertyChanged;
    protected void Notify(string name) => PropertyChanged?.Invoke(this,new PropertyChangedEventArgs(name));
}

internal sealed class OptimizationRow(OptimizationDefinition item) : SelectionRow<OptimizationDefinition>(item, item.Recommended,
    Environment.OSVersion.Version.Build >= item.MinimumBuild)
{
    private string state = Environment.OSVersion.Version.Build < item.MinimumBuild ? "Unsupported build" : "Not inspected";
    public string Title => Item.Title;
    public string Category => Item.Category;
    public string State { get => state; set { state = value; Notify(nameof(State)); } }
    public string Description => Item.Description;
}
internal sealed class TempRow(TempCandidate item) : SelectionRow<TempCandidate>(item, selected: true)
{
    public string Category => Item.Category;
    public string FilePath => Item.FullPath;
    public string Bytes => Item.Length.ToString("N0");
    public string ModifiedUtc => Item.LastWriteUtc.ToString("yyyy-MM-dd HH:mm");
}
internal sealed class OptionalAppRow(OptionalApp item) : SelectionRow<OptionalApp>(item,canSelect:!item.Protected)
{
    public string Name => Item.Name;
    public string PackageName => Item.PackageName;
    public string Version => Item.Version;
    public string Type => Item.Kind.ToString();
    public string Scope => Item.Scope;
    public string Protection => Item.Protection;
}

internal sealed class StartupRow(StartupItem item) : SelectionRow<StartupItem>(item, canSelect: !item.Protected)
{
    public string Name => Item.Name;
    public string Command => Item.Command;
    public string Scope => Item.Location;
    public string Protection => Item.Protected ? "Protected" : "Selectable";
}
internal sealed class HistoryRow(ChangeRun run,string machineId = "",string userSid = "")
{
    public ChangeRun Run { get; } = run;
    public string When => Run.StartedUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss");
    public string Title => Run.Title;
    public string Scope => machineId.Length == 0 ? "Fixture / unchecked" : Run.MachineId == machineId && Run.UserSid == userSid ? "This PC / account" : "Different PC / account";
    public string Summary => $"{Run.Entries.Count(e => e.Status == EntryStatus.Applied)} applied / "
        + $"{Run.Entries.Count(e => e.Status is EntryStatus.Failed or EntryStatus.RolledBack or EntryStatus.RecoveryRequired or EntryStatus.RestoreFailed)} errors / "
        + $"{Run.Entries.Count(e => e.Status == EntryStatus.Restored)} restored"
        + (Run.FinishedUtc == null ? " / interrupted" : Run.WasCancelled ? " / stopped early" : "");
    public string Undo => Run.HasPendingUndo ? "Settings available" : "No pending backup";
}

internal sealed class BulkBindingList<T> : BindingList<T>
{
    public void ReplaceWith(IEnumerable<T> values)
    {
        T[] snapshot = values.ToArray();
        bool notifications = RaiseListChangedEvents;
        RaiseListChangedEvents = false;
        try { Clear(); foreach (T value in snapshot) Add(value); }
        finally { RaiseListChangedEvents = notifications; }
        ResetBindings();
    }
}
