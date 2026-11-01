# BoosterX 4.9.4 validation — 2026-11-01

Build host: Linux x64; pinned .NET SDK 10.0.402; bundled runtime 10.0.13.

| Check | Result |
| --- | --- |
| Debug / Release solution builds | No warnings/errors |
| Existing pure regression suite | 84/84 retained and passed |
| Footer layout | Progress fills available row beside Stop; equal insets, fixed caption width and measured responsive fallback |
| Idle lifecycle | Empty 0% at initialization and after the common success/error/cancel finally path |
| Active progress | Existing monotonic work/native percentage model; unknown phases animate with a 0% caption |
| Attribution | Made by Skypixel Framework in the sidebar |
| Accepted renderers and feature backends | Preserved against 4.9.3 |
| Native rendering/interaction fixtures | 36 compiled; not run here |
| Footer state checks | Idle/unknown/intermediate/complete values, styles, fill and reset prepared |
| Native page/size matrix | 143 prepared; cycles footer states and checks both outer edges, insets, gaps, centers and attribution; not run here |
| Portable x64/ARM64 | Built; PE/version/manifest/icons/embedded assemblies/runtime inspected |
| ZIP CRC, paths, hashes and shared-source equality | Verified during packaging |
| Extracted source ZIP build and pure checks | Clean rebuild; 84/84 passed |

Only transient footer presentation is reset. Durable History, per-action results,
Activity logs, native command progress and Stop semantics remain. The core
OperationProgress model is unchanged. Windows servicing/uninstallers are never
killed to reset a display. Cleanup and Repairs retain their accepted behavior.

Footer checks use actual controls on Windows. They reject stale idle fill,
incorrect percentage/value/style, unequal outer insets, track/caption overlap,
clipped text, a short progress panel leaving unused row space, mismatched centers
and missing attribution. Symmetry assertions allow at most one device pixel for
centering an even-height track in an odd-height measured row.

## Limits

Actual Windows rendering, physical input, DPI transitions and Windows operations
were not executed on this Linux host. Native fixtures/page cases are prepared
and compiled, not passed Windows results. Final visual acceptance, including
physical pixel alignment, requires WINDOWS-TESTING.md on Windows. No Windows
optimization, uninstall, real cleanup or maintenance was run on the build host.
