# BoosterX 4.9.5 validation — 2026-11-01

Build host: Linux x64; pinned .NET SDK 10.0.402; bundled runtime 10.0.13.

| Check | Result |
| --- | --- |
| Debug / Release solution builds | No warnings/errors |
| Previous pure regressions | 84 retained and passed |
| New tray close-intent regressions | 5 passed |
| Combined pure suite | 89/89 passed |
| Window X | Cancels closing, posts hide, removes taskbar window; does not cancel active work |
| Tray icon | Persistent independently of popup preferences; restores same window/state |
| Explicit Quit | Idle actual exit; busy cooperative stop and deferred actual exit |
| Startup/elevation/shutdown | Actual close paths preserved; not redirected into hidden orphan instances |
| Hidden logging/errors | Bounded logs retained; hidden grid rebuild/modal errors suppressed |
| Accepted UI, footer, renderers and Windows backends | Preserved against 4.9.4 |
| Native rendering/interaction fixtures | 36 compiled; not run here |
| Native tray lifecycle fixture | 5 scenarios prepared; not run here |
| Existing native page/size matrix | 143 prepared; not run here |
| Portable x64/ARM64 | Built; PE/version/manifest/icons/embedded assemblies/runtime inspected |
| ZIP CRC, paths, hashes and shared-source equality | Verified during packaging |
| Extracted source ZIP build and pure checks | Clean rebuild; 89/89 passed |

Pure TrayClosePolicy tests cover active/idle user close, explicit idle quit,
wait-then-close during busy quit, system/application shutdown and unavailable
tray fallback. Actual form wiring also preserves the distinction between a
queued quit and permission to dispose the window; X can still hide while waiting.
Menu quit and hiding are posted after event dispatch, avoiding disposal or
handle recreation in the middle of a context-menu/window-close callback.

The native isolated form fixture exercises hiding, maximized restore, active
work without cancellation, menu Quit cancellation and final form/icon cleanup.
Popup preference independence and OS tray placement still require real Windows
acceptance. Closing X does not add autostart entries or start new maintenance.

## Limits

Actual Windows notification-area behavior, physical clicks, shutdown, elevation,
DPI/monitor transitions and Windows operations were not executed on this Linux
host. Native fixtures/page cases are prepared and compiled, not passed Windows
results. Use WINDOWS-TESTING.md for final platform acceptance. No optimization,
uninstall, real cleanup or servicing was run on the build host.
