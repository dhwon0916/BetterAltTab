# BetterAltTab

A small, per-monitor Alt+Tab switcher for Windows 10 and 11. C# / .NET 8 WinForms, native Win32 interop, no third-party runtime packages, no administrator privileges, no injection and no idle polling.

## Build and run

Install the [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) (a newer SDK can also build this target). In PowerShell:

```powershell
./build.ps1
./BetterAltTab/bin/Release/net8.0-windows/BetterAltTab.exe
```

The app appears in the notification area, possibly under the overflow arrow. Right-click it for **Enabled**, **Settings**, or **Exit**. Only one instance runs per login session. Exit releases all hooks and immediately restores native behavior.

The default gesture is **Alt+Tab**. Hold Alt and press Tab again to advance; Shift reverses; releasing Alt activates the selection. Enter also commits; Escape cancels. The monitor is captured at the start of each gesture, so moving the mouse mid-gesture does not move the list. When focus is on another monitor, the first forward step selects the target monitor’s most recent window. With no eligible windows, the overlay shows an empty state and commit does nothing. The candidate list is captured per gesture; destroyed windows are removed, and a moved selection is not activated on the wrong monitor.

Settings provide mouse/focus monitor selection, minimized-window inclusion, overlay visibility, start at login, and an Alt-based cycle key (Tab, backtick, or listed function keys). Shift remains the reverse modifier and Alt release remains the commit gesture. Assigning a different key leaves ordinary Alt+Tab native and takes over the chosen app shortcut instead. Arbitrary modifier combinations are intentionally not supported. Ctrl+Alt (including AltGr) and Windows-key chords are passed through. Avoid shortcut conflicts with other switchers or keyboard remappers.

## Switcher features

Settings are organized into **General**, **Displays & size**, and **App exclusions** pages, with Save/Cancel always available. The size slider and percentage field stay synchronized. Display choices show their resolution and primary/disconnected status. **Add application…** lets you choose executable files instead of typing names. Settings and troubleshooting follow the Windows light/dark preference when opened.

The switcher has a separate search/display header, window count, hover highlights, close-button feedback, and full-title tooltips. Troubleshooting shows timestamped events in a table, a recording indicator, and a clear empty state.

- **Navigation:** while holding Alt, Left/Right move one card and Up/Down move by the current grid column count. Enter selects; Escape dismisses. Tab and Shift+Tab continue to work.
- **Search:** while holding Alt, type to filter titles or executable names, case-insensitively. Backspace edits the query. No matches means nothing is activated. Queries reset each gesture and are never logged. Direct characters use the foreground keyboard layout; IME composition, dead-key composition, and AltGr-based text entry are not supported in the non-activating overlay.
- **Close:** click a card’s × to dismiss the switcher and send a normal `SC_CLOSE` request. Applications can prompt to save or refuse to close. This never terminates processes or auto-dismisses prompts.
- **App exclusions:** in Settings, enter executable names such as `mstsc.exe` or `game.exe`, one per line (commas/semicolons also work). When a gesture starts in a listed foreground app, the native shortcut passes through for the entire Alt-held gesture. Rules match exact executable basenames, ignoring case and `.exe`; they do not remove those apps from other windows’ switcher lists. If an exclusion list is configured and the foreground process cannot be inspected, the shortcut passes through conservatively. Apps are not automatically classified as games or remote desktops.
- **Per-display preferences:** select a display in Settings and enable **Override defaults for this display** to change its size and minimized-window inclusion. Other displays inherit **All displays (defaults)**. Disabling an override restores inheritance. Saved display names can change if Windows renumbers monitors.
- **Live size preview:** changing size shows a temporary, non-interactive switcher preview, positioned beside Settings when space permits. It hides after two seconds. Use **Preview size** to show it again, or turn live previews off. Save applies changes; Cancel discards them.
- **Troubleshooting:** tray → **Troubleshooting** → **Record diagnostics**, reproduce the problem, then **Refresh** and **Export**. The last 1,000 events are retained in memory: window handles, filtering reasons, monitor selection, exclusion matches, close requests, and activation attempts/results. No titles, search text, screenshots or typed keys are recorded. Recording is off by default and resets when the app exits. Nothing is written or uploaded unless you export. Windows does not always expose a definitive foreground-denial reason; the log distinguishes observed failures from possible security/foreground-lock causes.

## Portable, self-contained executable

To resize the switcher, open the tray icon's **Settings**, change **Switcher size (%)** (50–200%), and click **Save**. The next Alt+Tab uses that size; it is remembered across restarts. 100% is the enlarged default. The setting scales cards, previews, text and icons on top of Windows DPI scaling, while the overlay remains constrained to the monitor's working area.

```powershell
./build.ps1 -SelfContained                     # x64
./build.ps1 -SelfContained -Runtime win-arm64  # ARM64 cross-publish
```

Copy `artifacts/win-x64/BetterAltTab.exe` (or the ARM64 counterpart) to a permanent folder such as `%LOCALAPPDATA%\Programs\BetterAltTab`, then run it. A self-contained build bundles .NET and requires no separate runtime installation; it is larger than the framework-dependent build. It extracts native runtime components to the normal .NET bundle cache. The framework-dependent build requires the .NET 8 **Desktop Runtime** and its adjacent output files. x86 is not supported.

Enable **Start with Windows** only after placing the executable in its permanent location. Startup is per-user via `HKCU\Software\Microsoft\Windows\CurrentVersion\Run\BetterAltTab`; it stores the quoted executable path. Settings are atomically saved to `%LOCALAPPDATA%\BetterAltTab\settings.json`. Disable startup before moving or deleting the executable. To uninstall, disable startup, exit, delete the program folder, and optionally remove that settings folder. There is no installer or service.

## Architecture and Windows APIs

| Component | Responsibility / APIs |
| --- | --- |
| `KeyboardHook` | `SetWindowsHookEx(WH_KEYBOARD_LL)` on a dedicated STA message-loop thread; consumes cycle down/up events and queues commands to the UI thread. `GetAsyncKeyState` checks modifiers; injected events pass through. |
| `WindowService` | `EnumWindows`, visibility/style checks, `GetAncestor(GA_ROOTOWNER)`, `GetLastActivePopup` owner-chain filtering. `WS_EX_APPWINDOW` opt-in; `WS_EX_TOOLWINDOW` and `WS_EX_NOACTIVATE` excluded. |
| Monitor detection | `GetCursorPos` or foreground window; `Screen.AllScreens` and largest intersection with `DWMWA_EXTENDED_FRAME_BOUNDS` (`GetWindowRect` fallback). Negative screen coordinates work. Ties use enumeration order. |
| Minimized placement | Last observed visible monitor is cached. Windows already minimized at launch use `Screen.FromHandle` / the OS’s monitor association. |
| Virtual desktops | DWM cloaking and the supported `IVirtualDesktopManager.IsWindowOnCurrentVirtualDesktop` method exclude other desktops. If COM is unavailable, DWM filtering remains. |
| `MonitorHistory` | Independent per-display ordered handle lists; foreground events promote only that display’s window. Unknown windows initially follow enumeration/Z-order. History is in-memory, not persisted across process restarts. |
| `WindowEventMonitor` | Owns out-of-context `SetWinEventHook` subscriptions and coalesces repeated location events per window while preserving foreground-event order. `SystemEvents` handles display/session changes. |
| `LatestOperation` | Cancels superseded activation/close requests while keeping their cancellation sources alive until awaited work finishes. |
| `WindowActivation` | Awaits `ShowWindowAsync(SW_RESTORE)`, requests `SetForegroundWindow`, verifies actual foreground ownership, and retries with a temporary `AttachThreadInput` connection to the foreground thread. Connections are detached in `finally`, never held across an await, and skipped for hung apps. New gestures cancel pending activation. Failure flashes the taskbar button and shows a tray notice. |
| `SwitcherOverlay` | Non-activating, topmost Windows-style thumbnail grid; PerMonitorV2 DPI; rounded cards, system light/dark theme, titles, icons and blue selection border. `DwmRegisterThumbnail` / `DwmUpdateThumbnailProperties` supply live, aspect-preserving previews without screenshot polling. Cards can be clicked to activate. Oversized lists page around the selection. Thumbnail handles are released on dismissal. |
| Tray / settings | WinForms `NotifyIcon` recreates the notification icon after Explorer’s `TaskbarCreated` broadcast. User settings and HKCU startup integration require no elevation. |

The hook callback performs no enumeration, icon retrieval, COM calls or rendering. There is no idle polling. Activation uses short, bounded asynchronous waits while restoring/verifying focus. Preview composition is handled by DWM; protected or unavailable previews fall back to app icons, and minimized apps may expose their last rendered frame. Idle CPU should be negligible; window drag/location events still cause tracking work. The UI resembles the Windows task switcher but remains a custom overlay, not Explorer's own switcher.

Relevant Microsoft documentation: [keyboard hooks](https://learn.microsoft.com/windows/win32/winmsg/lowlevelkeyboardproc), [foreground activation](https://learn.microsoft.com/windows/win32/api/winuser/nf-winuser-setforegroundwindow), [virtual desktop membership](https://learn.microsoft.com/windows/win32/api/shobjidl_core/nf-shobjidl_core-ivirtualdesktopmanager-iswindowoncurrentvirtualdesktop), [window events](https://learn.microsoft.com/windows/win32/api/winuser/nf-winuser-setwineventhook).

Search uses [`ToUnicodeEx`](https://learn.microsoft.com/windows/win32/api/winuser/nf-winuser-tounicodeex) with its non-mutating flag (Windows 10 version 1607+). Close buttons post [`WM_SYSCOMMAND / SC_CLOSE`](https://learn.microsoft.com/windows/win32/menurc/wm-syscommand). Foreground app exclusions use limited-access process identity queries and do not enumerate processes or read files in the keyboard callback.

## Reliability boundaries

This is a custom task switcher, not a modification of Explorer’s Alt+Tab list. Windows does not expose its exact eligibility rules; the implementation uses visible top-level/root-owner windows, style exclusions, cloaking, and desktop membership. Some unusual app frameworks may differ from Explorer, and browser tabs are not separate windows here.

Normal applications work without elevation. Elevated applications, protected windows, UAC’s secure desktop, foreground-lock rules and other input hooks can block interception, icon access or activation. The app does not try to bypass these protections. Secure-desktop/session/display changes cancel the current gesture. Test your specific applications before using it as your only switcher.

Borderless fullscreen windows are eligible. Exclusive fullscreen games, anti-cheat software, remote sessions and raw-input applications may prevent the overlay or hook from behaving like native Alt+Tab; pause the app from the tray in those cases. This is not a secure-desktop replacement.

Display changes cancel the gesture and refresh monitor associations. Display device names are session identifiers, not guaranteed physical EDIDs: Windows can rename them after topology changes, so MRU continuity across disconnects is best-effort. Minimized windows with no observed visible bounds rely on Windows’s monitor association. DPI changes are handled by WinForms PerMonitorV2; mixed-DPI hardware testing is still required.

Windows can silently remove a low-level hook if its thread misses the OS timeout. The dedicated thread minimizes this risk, but there is no supported hook-health query; restart the app if interception stops. Alt-only menu handling can differ between applications because modifier events are passed through; verify menu-heavy applications. No synthetic modifier events are injected into other applications.

## Verification

```powershell
dotnet run --project tests/BetterAltTab.Tests.csproj -c Release
./BetterAltTab/bin/Release/net8.0-windows/BetterAltTab.exe --smoke-test
./BetterAltTab/bin/Release/net8.0-windows/BetterAltTab.exe --activation-test
./BetterAltTab/bin/Release/net8.0-windows/BetterAltTab.exe --feature-test
./BetterAltTab/bin/Release/net8.0-windows/BetterAltTab.exe --ui-test
./BetterAltTab/bin/Release/net8.0-windows/BetterAltTab.exe --lifecycle-test
```

The dependency-free test runner checks per-monitor history, geometry and thumbnail layout at 100%, 150% and 200% scales, including narrow/tall displays. Smoke mode briefly starts the tray and real Win32 hooks with interception **disabled**, renders an overlay, captures the composed preview to `artifacts/switcher-preview.png`, records thumbnail count, and exits after 1.2 seconds. Close the regular instance first (exit code 2 means another instance exists). Smoke errors produce `smoke-error.txt` and exit code 1.

The opt-in activation check creates two temporary fixture applications and briefly changes focus between them. It verifies actual foreground ownership, minimized restore and cancellation, closes the fixtures and attempts to restore the original foreground window. Results go to `artifacts/activation-test.txt`; failure returns exit code 1. It tests the activation service, not physical keyboard delivery or elevated apps. The fixture mode `--test-window` is internal to this harness.

The opt-in feature check tests settings serialization without saving user settings, live preview lifetime/placement, bounded diagnostics, and real hook delivery through uniquely tagged synthetic keystrokes in a temporary test window. It checks arrows, search characters, Backspace, Enter, Escape, Alt release and exclusion latching. It briefly changes foreground focus, releases Alt in cleanup, then closes test windows. Normal operation ignores synthetic input; tagged-input acceptance is enabled only for this harness's hook instance. Results go to `artifacts/feature-test.txt`.

See [TEST_PLAN.md](TEST_PLAN.md) for hardware and interactive acceptance checks. Compilation, pure tests and a startup smoke test do not establish that all applications or multi-monitor configurations work correctly.

`--ui-test` renders each settings page and troubleshooting state and checks size-control synchronization, draft per-display overrides, and Save visibility. It installs no keyboard hook and does not save settings. Renders and results are saved in `artifacts/ui`. The switcher render uses `DrawToBitmap` for its layout; DWM-composed thumbnail contents are not captured by that method.

`--lifecycle-test` repeatedly starts and disposes disabled keyboard hooks, event subscriptions and overlays. It checks shutdown, thumbnail cleanup, reuse of thumbnail geometry, and GDI resource growth. Its short disabled-hook CPU sample is diagnostic information, not a production performance benchmark. Results go to `artifacts/lifecycle-test.txt`. The pure runner also covers cancellation ownership, event coalescing, session isolation and Unicode Backspace.

Selection-only overlay updates reuse DWM geometry and cached fonts. Theme colors use a cached Windows preference refreshed when a window opens. Window snapshots resolve each process name once per snapshot, and startup seeds monitor placement in one enumeration pass.
