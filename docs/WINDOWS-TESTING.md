# Native Windows acceptance for BoosterX 4.9.2

Use isolated Windows 10 (19041+) and Windows 11 installations. Record OS build,
architecture, user/admin identity, work area, DPI, result and restore outcome.
The Linux build compiled these tests but did not execute Windows operations.

## Automated fixture and smoke commands

```text
dotnet run --project tests/BoosterX.Verification -c Release
dotnet run --project tests/BoosterX.RenderingChecks -c Release -- RenderingChecks
dotnet run -- --self-check
dotnet run -- --ui-check
```

84 pure checks are platform-independent. 36 actual-control fixtures require
Windows. --self-check reads native state and changes only owned temporary-file
fixtures. --ui-check uses fixture data and applies no optimization/uninstall.
It prepares nine pages at 13 monitor-constrained sizes plus filtered and empty
Repairs results at every size (143 cases), checks the
header/page geometry, protected selection and filtered-selection retention, and
writes control-only PNGs/latest.json under %LOCALAPPDATA%\BoosterX\UiCheck.
Native RichTextBox text can be missing from DrawToBitmap; inspect it live too.
No other desktop window is captured. Fatal check startup writes CheckFailure.

When using a portable GUI executable in PowerShell, wait for the process:

```powershell
$result = Start-Process .\BoosterX.exe -ArgumentList '--ui-check' -Wait -PassThru
$result.ExitCode
```

## Selector crash and interaction regression

- Reopen and choose options in Active adapter, DNS, Activity filter and preference
  category repeatedly, including switching away, outside click, F4/Alt+Down,
  arrows, Home/End/Page, Enter/Escape/Tab and replacing the bound adapter list.
- No ToolStripDropDown ObjectDisposedException may occur after a selection. The
  popup remains reusable until its selector is disposed; check owner close,
  disable/hide, movement, DPI change and application deactivation.
- Inspect selected object identity and DNS/filter behavior. Closed-field wheel
  input must scroll the page without choosing a setting. Read accessible value
  and popup items with a screen reader. Verify above/below work-area placement.
- Fixture checks include 100 repeated commit cycles and source replacement; UI
  dispatch exceptions count as test failures rather than default error dialogs.

## Layout, drawing and scrolling

- Test 1280x720 at 100% DPI, maximized, then smaller windows and 100/125/150/200/300%
  DPI. Include negative-coordinate monitors, taskbar/resolution changes and
  monitor disconnect/restoration. Base bounds must fit the actual work area.
- Keep the accepted rounded button fills/corners/focus states and icon clarity.
  Check caption controls, header toolbar, sidebar and all long button labels.
- Check page title/description/toolbars/grid left edges. Field heights should
  match at 40 logical pixels; action gaps are 8 and section gaps 12. In Activity,
  search text and checkboxes must align with the filter field. Verify wrapped
  toolbars and equally sized repair shortcut columns on narrow pages.
- Scroll over buttons, labels, grid cells/headers, report text and blank space
  with focus elsewhere. Nested targets consume their own wheel, then pass input
  to the outer page at an edge. Test fine deltas, Windows zero/one/three-line/page
  settings, Shift+wheel, horizontal wheels, track clicks and thumb dragging.
- Reach final variable-height grid rows and the last column. Test hidden/frozen
  fixture rows/columns, empty data, and shrinking data/columns; stale bars must
  disappear. Text wheel/keyboard/selection/copy must remain functional.
- Progress remains six logical pixels and dark. Percentage label must remain
  readable without overlap at compact widths. Unknown phases animate; completed
  action/native-command progress must never be presented as remaining time.

## Preferences and backup/history

- Search/category-filter all 34 choices. Select an item, filter it out, then apply
  on a test installation: hidden selections must remain in the reviewed plan.
  Clear/Recommended must operate across the inventory, not just visible rows.
- Inspect current stored values. Unsupported builds are unselectable. Test each
  registry preference individually; compare effective Windows behavior as well
  as stored values, including sign-out/policy/edition effects.
- Apply and restore original absent, string, expand-string, binary/multi-string
  and numeric registry values. Verify type/data, newest-first batch order,
  account/machine identity, interrupted actions, rollback and retry behavior.
- History must clear details when empty and preserve selected batch across
  refreshes. A deliberately corrupt copied fixture journal should be reported
  beside valid rows, while new mutations remain blocked. Do not delete real
  backups to clear a warning; inspect the failing file first.
- Restore points can fail/throttle; verify the result independently. They do not
  substitute for per-value snapshots or recover all app/deleted file data.

## Cleanup

- Cleanup must create no area selector, area checkboxes, presets or hidden
  expanded list. Scan cleanup areas must always evaluate all 22 fixed catalog
  categories. File preview selection remains available after the scan.
- On a roomy window, the title, explanation, action row and preview should fit
  without a long blank page. Resize small and large repeatedly; a scrollbar must
  disappear again when content fits. Test fine wheel input and keyboard focus.
- Reproduce the screenshot path: include Windows Update downloads, scan while
  BITS/wuauserv are stopped/running/pending/paused or inaccessible. A cache skip
  should log its reason without the service-allowlist exception or modal error.

- Verify each of the 22 category roots on Windows. Only eligible catalog roots/patterns
  appear. Use owned old/recent/read-only/locked/changed/link fixtures; deletion
  must recheck both ages, size, timestamps, final path and exclusive handle.
- Browser caches must be excluded while their process is running. Cookies,
  passwords, browser history/session storage and personal files must not appear.
  Test unknown/default browser profile folders and cache regeneration afterward.
- Windows Update download cache must be excluded while Windows Update/BITS runs
  or restart is pending; services are never stopped automatically. Verify all
  exclusions in Activity. Do not manually delete Windows Installer or WinSxS.
- Check bounded scan/cancel behavior, real visited/eligible counts, a new scan
  clearing an old preview, and file-based delete percentage. Disk
  savings are logical bytes, not a guaranteed physical-space or FPS measurement.

## Installed apps and native Windows actions

- Repairs must show individual buttons, grouped headings and wrapped details;
  there is no selection grid or inner scrollbar. Search by name/category/details,
  clear the search, and reach the final button by page scroll and keyboard.
  Confirm that each of the 28 buttons invokes its own documented handler.
- Scroll to the last repair action: the page must stop after its normal section
  margin. Start with narrow wrapped descriptions, grow wide, search Winsock,
  search a missing term, then clear. No stale height/blank scroll area may remain.
  Repeat at 100/125/150/200/300/400% DPI. The native fixture also starts a table
  at 8000px high to ensure old allocated height is not retained as content.
- Repeated preferred-size requests at unchanged width/DPI/content must reuse
  measured repair rows; the fixture verifies its internal measurement count.

- Inventory user/machine 32/64-bit Win32/MSI and current-account AppX packages.
  Verify names/versions, scope, protection, search and selected objects. Shared
  runtimes/system/security/driver items must remain unselectable. Review App details.
- Uninstall one expendable Store app and one owned MSI/desktop fixture; validate
  registered command revalidation, publisher prompts, cancellation, exit/restart
  codes and post-removal inventory. Test a changed/missing registration before
  execution. No arbitrary leftovers or shared dependencies may be force-deleted.
- Run each of the 23 command definitions separately. Compare real output/exit
  code. DISM/SFC progress must map to selected-action work; CHKDSK/drive optimization
  phases without a reliable overall percentage retain an animated current stage.
- Read-only diagnostic buttons must not alter settings. For state-changing maintenance,
  test failures and stop-after-current-action. Never infer repair success only
  from a process ending; review its output and native Windows verification.

## Network, Activity and unchanged functions

- Adapter list should omit low-level WFP filters, retain a real interface GUID and
  preserve selection on refresh. Diagnose with Ethernet/Wi-Fi/VPN/disconnection.
- On an isolated test adapter, verify DHCP/static DNS/order/registration snapshot,
  rename-before-restore, policy failures and IPv6 preservation. DNS changes are
  not bandwidth/FPS/game-ping benchmarks.
- Exercise Activity filters/search/follow/copy/export/clear and detail selection
  during output. Clear-view must preserve the raw log. Test notifications and
  foreground/background preference with Windows Do not disturb; no delivery is
  guaranteed. Unexpected UI exceptions should be recorded with full details.
- Repeat startup disable/restore, protected entries, high-performance AC/battery
  behavior, legacy service recovery, close-while-busy and original typed restore.
  Benchmark the same workload/conditions before and after changes.
