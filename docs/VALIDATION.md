# BoosterX 4.9.2 validation — 2026-10-24

Build host: Linux x64; .NET SDK 10.0.402; bundled Core/Windows runtime 10.0.13.

| Check | Result |
| --- | --- |
| Debug / Release solution builds | No warnings/errors |
| Existing pure regression suite | 84/84 retained and passed |
| Screen/DPI/taskbar geometry | Prior 312 combinations retained |
| Cleanup scan API | Always evaluates the complete 22-area catalog; no selected-area parameter |
| Cleanup UI | No area checkbox/list/preset models or controls created; scan, file preview and confirmed deletion retained |
| Repair actions | Same 28 buttons and handlers; accepted drawing/icons retained |
| Page extent | Current viewport probe; intrinsic preferred height for tables without percent rows |
| Repair row work | Cached text/positions by width and DPI; invalidated on content/search/font/DPI changes |
| Accepted button/checkbox/icon/selector rendering | Byte-preserved against 4.9.1 |
| Startup/network/power/service mutation code and native signatures | Byte-preserved against 4.9.1 |
| Native rendering/interaction fixtures | 36 compiled; not run here |
| Actual Form1 feature fixtures | No cleanup area controls; 28 repair actions; search/protection/selection fixtures prepared |
| Native page/size matrix | 143 cases prepared, including filtered/empty Repairs at 13 sizes; not run here |
| Portable x64/ARM64 | Built; PE/version/manifest/icons/embedded assemblies/runtime inspected |
| ZIP CRC, safe paths, source hashes and shared-source equality | Verified during packaging |
| Actual source-ZIP extraction/build/pure tests | Clean rebuild; 84/84 passed |

The previous scroll calculation probed using the old content height and then
summed allocated table row heights. That could retain surplus space as scrollable
content. Measurement now starts with the current viewport; natural-height pages
use GetPreferredSize(width, 0). Percent-row minimum/weight handling is retained.

The obsolete cleanup-checkbox fixture is replaced by an actual-control stale
height regression, including an initial 8000px table, wide/narrow resize cycles,
a matching repair search and empty results. Additional native fixtures check
repeated text-size requests do not remeasure unchanged rows, and the compact
Cleanup preview loses its scrollbar when it fits again. Form checks reject
cleanup area controls and trailing scrollable repair space.

The pure suite retains all durability/rollback/restore, scanner age/link/bound,
service idle-state, geometry, palette, icon, precision wheel, dropdown placement,
uninstaller, progress and history checks. No performance benchmark is inferred
from reducing control creation or cached layout work.

## Limits

Actual Windows rendering, physical input, native control measurement, DPI
transitions and Windows operations were not executed on this Linux host. The
36 native fixtures and 143 layout cases are compiled/prepared, not passed Windows
results. Pixel-perfect acceptance and old-PC responsiveness still require the
Windows checks in WINDOWS-TESTING.md. No optimization, app uninstall, real file
cleanup or Windows maintenance was performed on the host.
