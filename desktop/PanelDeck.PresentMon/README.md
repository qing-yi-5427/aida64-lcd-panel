# In-process display frame capture

PresentMon 2.6.0, MIT, pinned commit `e13fce6acdb55a808fd8318175a56863e532d95f`.
Vendored source provenance and original SHA-256 hashes: `../third_party/PresentMon/upstream.json`.

The DLL runs inside the existing authorized collector, with a statically linked C++ runtime. There is no extra EXE, installed service, overlay, driver, or CSV writer. The host selects the foreground fullscreen process. The analysis retains required system/DWM events; it filters output to the target process and rejects known other windows. Display and frame-type tracking are enabled. GPU, GPU video, input, PC latency, application timing and shader compilation tracking are disabled.

Only completed, non-lost, successful Presented events contribute their Displayed timestamps; repeated frames are excluded. Generated display entries are included without requiring that the native/generated identity be known. Output is sorted by display timestamp. The managed bounded window deduplicates timestamps, accepts late entries and chooses one active swap chain. No display data means unavailable, never a submitted-FPS fallback. ETW loss or overflow clears the window; native failure stops the trace. This is system-reported presentation timing, not optical measurement of the panel.

PanelDeck 2.8.0 derives average displayed frame time from the same two-second FPS window. Its 1% Low is frequency divided by the mean of the slowest ceil(1%) intervals in the last 30 seconds, with at least 100 intervals and full-window coverage required. A ring stores at most 32768 timestamps per chain (eight chains maximum), plus one reusable sorting buffer. Sorting occurs at most once per second. Insufficient capacity, warmup or missing data gives no low value; display gaps of three seconds restart history. No extra native tracking is enabled for these statistics.

## Build

Use an x64 MSVC C++20 toolset and Windows SDK. No C++ runtime installation is needed on the target PC. Run `desktop/build-presentmon.ps1 -MsvcRoot <VC/Tools/MSVC/version> -SdkIncludeRoot <Windows Kits/10/Include/version> -SdkLibRoot <Windows Kits/10/Lib/version>` before the normal .NET build. The script also supports the portable build-only toolset under ignored `artifacts/native-tools/` used in this workspace. It never installs system components.

Build output is `bin/PanelDeck.PresentMon.dll` (ignored by Git). The app project copies it for build/publish and fails clearly if it is absent. `build-desktop.ps1` packages the MIT license. The native sources are deliberately pinned; update them with a provenance/hash review and replay/live tests before changing the release.

## Host adaptations

- Unmodified upstream event analysis, event manifests and frame-generation association.
- Small local adapters replace the upstream service logging/exception framework and offline precision waiter. Standard exceptions remain; no diagnostic stack collector or high-resolution timer is created. Only realtime sessions are exposed.
- PresentMonTraceSession adds a noexcept callback wrapper with an atomic error flag; exceptions cannot cross the Windows callback boundary.
- Realtime ETW uses 64 KiB buffers, 16 minimum / 128 maximum, no per-CPU allocation, one-second flush. Lost events/buffers and present-ring overflow are queried and invalidate output. The upstream broad Performance keyword exclusion remains intact.
- ABI 1 has fixed-size 16-byte display entries and 32-byte diagnostics, verified by Windows tests.

## Verification

Run `dotnet run --project desktop/PanelDeck.WindowsTests -c Release`. This includes screen timestamp accounting, delayed generated frames, duplicate timestamps, independent swap chains, staleness, invalid data and DLL loading. These synthetic checks are not evidence of game/frame-generation compatibility.

Run `desktop/build-presentmon.ps1 -TestOnly` for native bridge tests against synthetic completed PresentMon events. It checks frame filtering, ordering, diagnostics, capacity and ABI without starting ETW, changing the foreground window or replacing the production DLL.

Explicit live diagnostic (requires trace privileges and an active fullscreen target): `PanelDeck.WindowsTests.exe --display-probe <output.json> 30`. It writes per-second display readings and loss diagnostics, plus process CPU/memory. Game performance impact requires controlled capture-on/off runs in the same scene; collector CPU alone does not measure that impact.
