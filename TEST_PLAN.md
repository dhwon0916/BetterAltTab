# Manual acceptance plan

## Added features

- Add a fixture executable to exclusions. Start Alt+Tab in it and keep Alt held as focus moves: the entire gesture stays native. Release Alt, start from another app: custom switching resumes. Remove the rule and retest. Try an inaccessible/elevated process with exclusions configured.
- Hold Alt, navigate with all arrow keys across pages, press Enter, then release Alt: activation occurs once. Escape must cancel without a later commit.
- Type an app name or title, use Backspace, test no matches, clear the query, and release Alt. No query keystrokes should reach the foreground app. Verify direct-character layouts; IME/dead-key composition is outside the supported search scope.
- Click × on an expendable unsaved document. Its normal save prompt must appear; cancel the prompt and verify the document survives. Test a hung app and an elevated window: never terminate the process.
- Change the size with live preview on and off. Preview must leave the settings controls accessible when space permits, hide after two seconds and close with Settings. Cancel must not save edits.
- Give two displays different sizes/minimized rules. Save/relaunch and verify both. Disable one override and confirm it inherits defaults. Disconnect/reconnect and confirm no crash; Windows may rename display identifiers.
- Enable troubleshooting, reproduce a missing window or activation failure, Refresh and Export. Check filtering and activation reasons, absence of titles/search text, the 1,000-event limit, Clear and disabled recording.

Run with ordinary privileges on Windows 10 and Windows 11. Keep the tray Exit command accessible. Record OS version, display topology/scales, application names, configured hotkey and any mismatches. These checks are manual and are not claimed as completed by the automated test runner.

| Scenario | Steps and expected result |
| --- | --- |
| Two monitors | Open A/B/C on display 1 and D/E on display 2. Put cursor on each display and cycle. Only that display’s windows appear. Cycle D/E repeatedly; A/B/C order stays unchanged. With focus still on display 2, move cursor to display 1: first forward step selects its MRU window. |
| Three monitors | Repeat with a third display placed left of and above the primary (negative coordinates). Each list is independent and the overlay stays in its working area. |
| Focus mode | Enable focused-window monitor setting. Move cursor elsewhere. Alt+Tab stays on the focused window’s display. |
| Mixed DPI | Use 100%, 150%, 200% displays. Start gestures on each; inspect icon/text clarity, clipping, selection and centering. Change scaling while running and retest. |
| Minimized | Minimize windows on both displays; include/exclude minimized windows. Included windows retain their observed monitor and restore on commit. Also launch the utility with windows already minimized. |
| Spanning / moved windows | Drag a large window across the boundary. It changes lists when its largest visible frame area crosses. Move a previously used window, then verify both MRU lists. Move/close a selection during a gesture: it must not activate on the wrong monitor. |
| Disconnect/reconnect | Disconnect the active monitor during a gesture. Overlay closes with no crash/stuck input. Reconnect, enumerate again, and verify current positions; history continuity is best-effort if device names change. |
| Reverse cycling | Shift+Alt+Tab initially and during a gesture wraps backwards. Release Shift while holding Alt and continue forward. Test both Alt keys and both held together. |
| Rapid input | Tap Alt+Tab rapidly 50 times; hold Tab for autorepeat; release Alt before Tab. No native switcher leaks through, stuck modifiers or stale overlays. Verify normal typing afterwards. |
| Cancel / empty list | Escape cancels without activation. No eligible windows consumes the gesture without crashing; one window selects itself. |
| Many windows | Open more than eight windows. Cycle both directions; selection stays visible and all windows can be reached. Duplicate titles remain separate entries. |
| Window filtering | Check tooltips, helper dialogs, owned modal dialogs, taskbar, desktop, always-on-top overlays, browser windows, Explorer and packaged apps. Compare with native Alt+Tab and document differences. |
| Virtual desktops | Put windows on two virtual desktops. Only the current desktop appears. Change desktops during a gesture and verify no other-desktop activation. |
| Fullscreen | Test video, borderless fullscreen and an exclusive fullscreen game. Document unsupported cases; pausing must restore native behavior. |
| Elevated / UAC | Try switching into and out of an elevated app. Trigger UAC during a gesture; expect cancellation, no overlay on secure desktop, and no security bypass. Check denied activation feedback. |
| Explorer restart | Restart Explorer from Task Manager. Tray icon returns automatically; hook and switching continue. |
| Pause / exit | Disable using tray. Native Alt+Tab works unchanged. Re-enable, then exit during a gesture; no orphan overlay or installed hooks. |
| Settings | Save/relaunch each setting; verify persistence. Test Alt+backtick and a function key, reverse and commit. Ctrl+Alt/AltGr and Win chords remain native. Check app menus do not open unexpectedly on Alt release. |
| Startup | Install to final path, enable startup, log out/in. Exactly one instance starts. Disable, log out/in: none starts. |
| Lock / sleep | Lock or sleep mid-gesture. Resume without stale UI or activation. |
| Resource use | Observe Task Manager while idle for five minutes, then switch repeatedly and drag windows. Idle CPU stays near zero and handles/memory do not grow continually. Test one hung application to confirm icon lookup is bounded. |

Automated tests cover ordering/geometry and thumbnail layout. The separate `--activation-test` harness verifies ordinary cross-process foreground changes, minimized restore and cancellation. Actual keyboard delivery, elevated activation rights, Explorer restart, secure desktop, mixed DPI and hardware changes still require these interactive checks.

For the thumbnail grid, verify live updates (for example a playing video), aspect-ratio preservation, light/dark theme, card click activation, and paging through more windows than fit. Protected content may be blank and minimized windows may show their last rendered frame.
