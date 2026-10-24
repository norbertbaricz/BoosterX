# BoosterX 4.9.2 migration

Existing 4.x data directory, settings fields, journal format 1, stable optimization
IDs and reverse restoration order remain. Startup handling and the accepted
renderer/icon/sidebar/header implementation are preserved. The main project is
still root Form1 with normal `dotnet run` and x64/ARM64 portable profiles.

| Area | Implementation |
| --- | --- |
| Popup failure | Reusable per-selector popup; no disposal inside Closed |
| Preferences | Core PreferenceCatalog -> Windows registry mapping; searchable category/status view |
| Cleanup | CleanupCatalog + scanner predicate/progress; final deletion revalidates allowed root/pattern/idle state and exclusive handle |
| Apps | Current-account AppX + registered user/machine Win32/MSI; explicit protection/scope and native publisher removal |
| Repairs | 28 individual responsive buttons; fixed Core RepairCatalog arguments and existing command/exit handling |
| Progress | OperationProgress, action counts and native percentage; retained terminal state; unknown phases animate |
| History | Inspect reports partial inventory/errors; strict List/engine guards retained; typed restore verification |
| UI | Form1.Maintenance.cs removes cleanup selection controls; DetailListPanel caches repair rows; DarkScrollPanel measures natural height |
| Validation | 84 pure checks; 36 prepared native fixtures; 143 prepared page/size cases |

Original recommended preferences remain selected. Cleanup includes all 22 areas
for automatic scanning; area customization is removed, while file preview and confirmation remain. No repair or
app uninstall runs automatically. New preferences remain opt-in. Old setting snapshots stay on the supported registry target list.
Application removal and cache cleanup remain irreversible. Publisher uninstaller
results and Windows servicing output must be reviewed; no speculative leftover
purge is added.

Application.ThreadException now records unexpected interface errors through the
dark app UI. Test modes record fatal startup errors and exit instead of waiting
on a modal default error box. This is a reporting boundary; the popup lifecycle
root cause is fixed separately.

The earlier cleanup error shown in Activity was caused by a service check. Cleanup called
ServiceManager.Capture for BITS, outside the service mutation allowlist. The new
CleanupServiceGate validates only fixed update service names and evaluates their
read-only status independently. The mutation allowlist and restore behavior are
unchanged. Unavailable/transitioning state excludes the update cache.

In 4.9.2 the CleanupService.Scan API always uses the complete catalog; it no
longer accepts an area-selection collection. The UI creates no cleanup area
checkboxes, presets or expanded lists. The file preview still lets users review
eligible files before the existing confirmed deletion path.

Repairs retains its actions. DarkScrollPanel no longer feeds content.Height
back into measurement; natural-height tables use preferred content height.
DetailListPanel caches measured placements, with width/DPI/content invalidation.
