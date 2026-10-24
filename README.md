# BoosterX 4.9.2 — C# Windows Forms

Portable Windows 10 (build 19041+) / Windows 11 maintenance application, using
.NET 10, Form1 and the Visual Studio WinForms designer. Product text is English.
The accepted dark buttons, icon system, custom window frame and responsive
1280x720 base layout are retained.

## Run and publish

On Windows with the .NET 10 SDK, extract the source archive and open its BoosterX
folder (the one containing BoosterX.csproj):

```text
dotnet run
dotnet build BoosterX.sln -c Release -m:1
dotnet publish BoosterX.csproj -c Release -p:PublishProfile=Portable-win-x64
dotnet publish BoosterX.csproj -c Release -p:PublishProfile=Portable-win-arm64
```

Normal development uses the root project and does not require an elevated
terminal. Applying changes requests an administrator restart. Recheck selections
after restart; user-scoped settings target the account used for elevation.
Portable executables contain their .NET runtime. Extract the full ZIP and use
Portable/win-x64/BoosterX.exe or Portable/win-arm64/BoosterX.exe. The source ZIP
contains the complete project, tests and documentation without the binaries.
Open Form1.cs -> View Designer in a Visual Studio version supporting .NET 10.

## What changed in 4.9.2

- Cleanup scans every one of its 22 fixed catalog areas automatically. The area
  selector, presets, expandable sections, checkbox models and hidden area controls
  are removed. The normal Scan, file preview, Delete, Recycle Bin and Storage
  settings actions remain. File-level review and deletion confirmation remain.
- Repairs keeps the accepted 28 individual action buttons, category headings,
  descriptions and search. The button drawing, icons and action handlers remain.
- Fixed scroll extent calculation: page measurement no longer starts with the
  previous tall extent. Natural-height tables use intrinsic preferred height,
  rather than allocated row heights that can include surplus blank space.
  Search and wide/narrow resize cycles can now shrink the extent again.
- Repair text measurements and row positions are cached for the current width,
  DPI and content. Unchanged size queries reuse them. The cache is invalidated
  for search, font, DPI and relevant control-content changes; unchanged bounds
  are not assigned again. The final action has no extra trailing row gap.
- Native fixtures now cover stale height, resize/search shrinkage, empty results,
  cached measurements and the compact Cleanup preview. The page matrix checks
  unfiltered, filtered and empty Repairs results at every tested size.

## Features retained from 4.9

- Fixed the Network/Activity selector crash: the popup was disposed synchronously
  inside ToolStripDropDown.Closed. Each selector now reuses one popup for its
  lifetime; disposal occurs when the selector itself is disposed. Native fixtures
  include 100 repeated open/commit/close cycles and source replacement.
- 34 reviewed preferences in searchable categories, with current-value inspection,
  support checks and a count of selections across all filters. Original seven
  IDs and three default recommendations are preserved; new choices are opt-in.
- 22 explicit cleanup categories: temporary files, DirectX/vendor shader caches,
  crash dumps, error reports, selected Explorer cache database names, closed
  browser/application caches and developer download caches. All areas start
  scanned automatically; review eligible files before deleting. See the cleanup rules below.
- Inventory of current-user AppX packages and registered Win32/MSI applications
  from current-user/machine 32/64-bit uninstall keys. Rows show type, scope,
  protection and version. Search and filtering preserve selected objects.
  Shared runtimes, Windows/system packages and security/driver entries are
  protected. Nothing is selected automatically.
- A searchable set of individual Windows diagnostic/maintenance buttons.
  Each description explains read-only status, state changes and restart/undo limits.
- Real work percentage next to the thin progress track. Completed actions and
  native DISM/SFC/package percentages contribute to progress; unknown phases
  animate. The final state is retained instead of resetting immediately to zero.
  Percentages describe work, not estimated time or guaranteed success.
- History displays backup scope, clears stale details and reports unreadable
  journals without hiding valid records. The engine still blocks new mutations
  if a corrupt/interrupted backup needs attention. Restored registry values are
  checked for their original type/data.
- Shared 40-logical-pixel fields, 8px action gaps and 12px section spacing; matched
  left edges and consistent repair shortcut widths. Native checks inspect page
  alignment. No pixel-perfect native acceptance is inferred from compilation.

## Preferences and restoration

Preferences cover desktop effects, game capture/Game Mode, suggestions, search,
Explorer, privacy, startup behavior, Edge background policies, input and power.
They are documented preferences with tradeoffs, not an arbitrary count of
performance registry tweaks. Some need sign-out or may be ignored by a Windows
build, edition or managed policy. Current-value inspection checks stored values;
it is not an FPS, latency, RAM or bandwidth benchmark.

Original values and types reach the per-account/per-machine journal before a
mutation. Failed reversible actions attempt rollback; failed rollback blocks new
changes. Restore the newest reversible batch first and work backwards. Existing
4.x journal format and data locations are retained. File/app removal and Windows
maintenance have no BoosterX undo. A Windows restore point can fail or be
throttled; it is separate from these setting snapshots and does not recover all
app data/deleted files.

## Cleanup rules

Only catalogued roots and exact cache patterns are eligible. Downloads, Documents,
Desktop, browser cookies/passwords/history/session storage, source projects,
Windows Installer, manual WinSxS deletion and Prefetch are excluded. Both creation
and modification must exceed TempMinimumAgeDays (7 by default). Recent, changed,
read-only/system, linked/reparse and locked files are skipped. Deletion rechecks
root/pattern, timestamps, size and the final non-following exclusive file handle.

Browser/application categories require their associated processes closed.
Windows Update downloads are offered only when Windows Update/BITS are stopped
and no restart is pending; BoosterX never stops these services automatically.
Category exclusions are logged. Shader/thumbnail/icon caches are recreated and
first launches/previews can become slower. Clearing cache is not a universal
performance improvement. Logical file bytes are reported, not a promised change
in physical free space. Scans remain bounded by settings and cancellation.

## Application removal

AppX uses PackageManager removal for the displayed account. Desktop/MSI uses the
publisher's registered uninstaller, revalidated immediately before launch; MSI
uses its product GUID and /norestart. Publisher dialogs can require interaction.
Installer cancellation/exit/restart results are recorded. Unsupported shell hosts
are left to Windows Installed apps. No Win32_Product query, arbitrary Program
Files deletion, shared dependency purge or speculative registry cleanup is used.

A publisher can intentionally keep shared dependencies or personal profiles;
there is no honest universal guarantee of deleting every trace. Use App details
to review identity, publisher, location, scope and registered command. Store /
app settings opens Store for packages and Windows Installed apps for desktop
entries. Native maintenance may request restart; no forced reboot is scheduled.

## Network, Activity and progress

Network lists active IP interfaces with stable GUIDs and omits low-level WFP
filter pseudo-adapters. Diagnostics are read-only. DNS changes preserve IPv6 DNS
and registration/source/order snapshots; managed/VPN DNS can reject changes.
DNS selection cannot guarantee lower game ping or more bandwidth.

Activity preserves bounded recent rows, status/search filters, complete selected
entry details, full-session export and notification preferences. Clearing the
view preserves the raw log. Unexpected UI errors are logged and shown in the
application's dark dialog rather than the default white WinForms error dialog.

Percentage uses completed action count plus supported native sub-action progress.
A command reporting 100% remains below overall completion until it exits. Unknown
phases use animation; a long unknown command can keep its current completed-action
percentage until it finishes. A 100% processed batch can still contain errors;
review History/Activity. Stop remains between actions and never kills servicing
or a publisher uninstaller halfway through its work.

## Layout and project files

Startup is 1280x720 logical pixels at 96 DPI, scaled and clamped to the monitor
work area. Sidebar widths remain 244/220/192 logical pixels. Compact Menu,
two-axis pages, pointer-based precision scrolling, nested edge handoff, custom
scrollbars and DPI/monitor adaptation are retained. System file pickers and
publisher uninstallers follow their own Windows/publisher UI.

| Source | Purpose |
| --- | --- |
| Form1.cs / Designer.cs / resx | Existing form, handlers and designer layout |
| Form1.Features.cs | Preference/app filters, spacing, progress and fixture checks |
| Form1.Maintenance.cs / Controls/DetailListPanel.cs | Fixed all-area cleanup and responsive cached repair buttons |
| Form1.Layout.cs | Responsive bounds, header/sidebar/footer and DPI |
| Form1.Icons.cs / Controls | Accepted icon/renderer system and dark selectors/search fields |
| src/BoosterX.Core | Durable journal/engine, pure catalogs, parsing, progress and scanner |
| Services | Windows registry, cleanup, installed-app, command, network and native implementations |
| tests | Pure verification and native control/interaction fixtures |

Easy-to-tune limits are in BoosterOptions.cs, UiPalette.cs and UiLayout.
PreferenceCatalog.cs, RepairCatalog.cs and CleanupCatalog.cs centralize the
reviewed choices. Data stays under %LOCALAPPDATA%\BoosterX (settings, Backups,
Logs, UI preferences). The full release includes runtime license notices.

## Verification

```text
dotnet run --project tests/BoosterX.Verification -c Release
dotnet run --project tests/BoosterX.RenderingChecks -c Release -- RenderingChecks
dotnet run -- --self-check
dotnet run -- --ui-check
```

The 84 pure checks run on supported .NET hosts. The 36 native rendering/interaction
fixtures and 143 page/size cases require Windows; they were compiled, not run,
in the Linux build environment. Native UI checks use fixture controls/data and
perform no optimizations. Bitmap output captures only fixture controls and can
omit native RichTextBox text. See docs/VALIDATION.md and WINDOWS-TESTING.md.

Both ZIPs have one BoosterX root, verified source hashes and identical shared
source bytes. The actual source ZIP is extracted and rebuilt during packaging.
Delivered x64/ARM64 single-file binaries were inspected for GUI architecture,
version, manifest, icons, actual managed assemblies and bundled runtime. They
are unsigned development builds, not a substitute for real Windows acceptance.
