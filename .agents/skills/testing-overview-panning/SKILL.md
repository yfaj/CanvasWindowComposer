---
name: testing-overview-panning
description: End-to-end test the CanvasWindowComposer overview/panning behavior on a live Windows desktop — window management, always-on-top (WS_EX_TOPMOST) handling, and taskbar rendering in the overview. Use when verifying pan/overview UI changes or overlay/DWM-thumbnail behavior at runtime.
---

# Testing CanvasWindowComposer overview / panning

CanvasWindowComposer maps every managed window onto an infinite virtual canvas and
renders the slice under the current camera. The "overview" is a DWM-thumbnail overlay
shown during **Panning** (transient, while a middle-drag is held) and **Zooming**.

## Build & run
```
dotnet build -c Release CanvasDesktop.csproj
Start-Process bin\Release\net8.0-windows\CanvasDesktop.exe
```
If NuGet restore fails, add the source once: `dotnet nuget add source https://api.nuget.org/v3/index.json -n nuget.org`.

## Triggering a pan programmatically (key lessons)
The pan starts on a **middle-button drag** only when `Alt` is held OR the cursor is over
the desktop/taskbar (`Source/System/RawMouseInput.cs`). To drive it from a script, inject
SendInput with:
- **Hold Alt (VK_MENU) for the whole drag+hold**, release only at the end.
- Encode relative mouse deltas as unsigned 32-bit ints (RawInput expects this).
- **Start the drag over a normal window** (e.g. Notepad) — empirically this triggers
  Panning far more reliably than starting over empty desktop, which was flaky.
- Use enough motion (≥ ~5px/step over ~25 steps). Tiny pans (~30px total) often failed
  to open the overlay.
- Use a long `-holdMs` (7–8s) so you can screenshot while the overlay is up (it's transient).

Reusable helpers (recreate if missing; they live outside the repo):
- `drag.ps1 -x1 -y1 -x2 -y2 -steps -holdMs -alt` — Alt+middle-drag pan.
- `probe.ps1` — a red 300x200 **TopMost** WinForms window ("TOPMOST PROBE"), a generic
  stand-in for docks like MyDockFinder (the code keys on WS_EX_TOPMOST, not process name).
- `autohide.ps1 -on 1|0` — toggle taskbar auto-hide via SHAppBarMessage(ABM_SETSTATE).
  Its `Write-Output ([int]$state)` line can throw a UIntPtr cast error AFTER the state is
  already set — harmless, ignore it (or cast via `[uint64]`).
- `movenp.ps1` — restore + move/size a Notepad window to a known rect (SW_RESTORE +
  MoveWindow + SetForegroundWindow), useful because Alt+Tab restore was unreliable.

## Confirming the overlay is actually active
On a solid-color wallpaper a Panning overlay looks identical to the static desktop
(PanningConfig draws the wallpaper at opacity 255). To prove the overlay is up, keep a
**managed window visible and watch it reproject** (move) during the pan. Don't rely on the
wallpaper. The bottom-right **mini-map** appearing is another activity indicator.

## What to verify
- **Always-on-top windows are fixed furniture**: a WS_EX_TOPMOST window must NOT pan (stays
  put), must appear exactly once (no DWM-thumbnail double), and must be absent from the
  mini-map. Managed (non-topmost) windows DO pan.
- **Auto-hidden taskbar**: with auto-hide ON, an *active* pan must draw NO taskbar at the
  bottom; run the same pan with auto-hide OFF as a contrast (taskbar IS drawn) to prove the
  overview renders it normally. Relevant gate: `TaskbarVisible && !_taskbarAutoHidden` in
  `Source/Overlays/Overview/OverviewThumbnails.cs`.

## Testing the camera glide (smooth focus transitions)
The glide (`AnimatedCameraGlider` + `CameraGlide`) only fires when a focused managed window is
**fully off every screen** (`ForegroundCoordinator.OnWindowFocused` → `IsOnAnyScreen` false).
To exercise it end-to-end:
- **Make motion observable for still screenshots**: temporarily bump `AnimatedCameraGlider.DurationMs`
  (e.g. 220 → 2500) and add `TrayApp.Log(...)` calls in `GlideTo`/`OnTick`/`Cancel`. Log lands at
  `bin/Release/net8.0-windows/canvas_debug.log`. **Revert these temp edits after** (they're
  uncommitted working-tree changes, so `git diff --stat` should be empty when done).
- **Push the window fully off-screen deterministically**: `movenp.ps1` to recenter first, THEN one
  big single Alt+pan (`drag.ps1 -x1 1100 -x2 30 -steps 45 -alt`). Multiple small pans drift and
  leave a few-px sliver on the left edge — and a sliver counts as on-screen, so the glide won't fire
  and you get an empty `canvas_debug.log`. **Always screenshot-confirm the window is 100% gone**
  before triggering; re-pan a bit more if any sliver remains.
- **Fire a real off-screen focus event** (NOT minimize/restore — that triggers reconciliation, not a
  focus glide): focus some *other* window, then `SetForegroundWindow` the off-screen one with
  `AttachThreadInput` (see `focus.ps1 -title Notepad`). A clean unmanaged intermediate is the
  **taskbar** (`FindWindow("Shell_TrayWnd")`, see `focustaskbar.ps1`) — it's not a managed window so
  it won't itself glide, leaving only the target window on screen in the "after" shot.
- **Assert from the log**: a real glide logs one `GLIDE start` + dozens of `GLIDE frame` lines with
  ease-out (big early deltas → sub-pixel late), ending `done=True` exactly at target. A snap would
  be 0–1 frames.
- **Cancel-on-input**: to catch it partway, inject the click from a **single PowerShell process** that
  also does the focus (`canceltest.ps1`) — launching separate `powershell.exe` per step adds ~1.5–2s
  latency (Add-Type compile + process start) so the click lands near the end. A cancel logs
  `GLIDE cancel at cam=(...)` preceded by `done=False` frames (never `done=True`).

## Gotchas
- **Shared desktop:** if a human is also moving the mouse, injected SendInput collides with
  it — ask them to keep hands off for ~20s during a capture.
- **"Windows disappear when moved"** is expected: dragging a window sends it to a canvas
  coord that may be off the current (panned) viewport. Restart the app to recenter the camera.
- Maximized windows don't visibly reproject during a pan — use a normal, moveable window.
- Camera state persists across pans; **restart the app** to recenter between test setups.

## Devin Secrets Needed
None. All testing is local on the desktop VM.
