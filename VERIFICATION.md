# Verification performed

## Optimization and cleanup — 2026-10-01

- Release build passed with warnings treated as errors: zero warnings/errors.
- 107 pure regression checks passed, including cancellation-source ownership, session isolation, Unicode Backspace and coalescing 10,000 location events into one pending batch.
- 61 lifecycle checks passed: 20 keyboard-hook start/stop cycles, 10 event-subscription cycles, 10 overlay cycles, stable thumbnail geometry during repeated selection refreshes, and bounded GDI resource growth. GDI objects increased from 3 to 15 across the overlay test, within the fixed cache allowance; this is a bounded stress check, not proof against every leak.
- All 10 UI checks, 17 feature checks and 9 cross-process activation/close checks passed on the current Release build. The guarded keyboard fixture successfully acquired foreground ownership in this run.
- The final self-contained x64 package also passed all 10 UI and 61 lifecycle checks (both exit code 0); publish completed with zero warnings/errors. Output: `artifacts/win-x64-optimized/BetterAltTab.exe`.
- Extracted activation, event monitoring, process identity, cancellation ownership and event batching into separate components. Fixed premature cancellation-source disposal and made resource cleanup idempotent. Reduced repeated registry reads, font allocation, DWM geometry updates, process-name queries and window enumeration.
- Tests did not save user settings or change startup registration. Physical keyboard delivery, elevated applications, mixed-DPI hardware, display hotplug and long-running production performance remain manual checks. The one-second disabled-hook idle sample is not a production benchmark.

## GUI refresh — 2026-10-01

- Reworked settings into three themed pages with persistent Save/Cancel, a synchronized slider/percentage field, readable monitor names and executable browsing.
- Reworked troubleshooting into a themed event table with recording status and empty state; polished the switcher header, typography, hover states and close-button feedback.
- Release build passed with zero warnings/errors. Ten UI checks passed: page rendering, size synchronization in both directions, inheritance/override draft isolation, Save visibility, diagnostic states and switcher layout registration.
- Visually inspected settings and diagnostics renders at 150% DPI, correcting clipped sidebar text, themed dropdowns and header layout. Inspected switcher GDI layout; four DWM thumbnails registered, but their composed contents are absent from the layout capture.
- UI checks do not send keystrokes, save user settings or start a persistent app instance. Previous foreground-activation limitations remain unchanged.

## Feature expansion — 2026-10-01

- Release build: zero warnings/errors.
- 93 pure regression checks passed: existing MRU/geometry/layout coverage plus session navigation, empty search, filtering, selection removal and executable rule normalization.
- Nine desktop activation/close checks passed, including process identity resolution and `SC_CLOSE` delivery to a disposable fixture process.
- Seventeen feature integration checks passed in the framework-dependent build: settings round trips and inheritance, old-settings defaults, bounded/disabled diagnostics, live-preview lifetime and placement, actual low-level hook routing of tagged synthetic arrows/text/Backspace/Enter/Escape/Alt release, and exclusion bypass latching across a full gesture.
- The final self-contained build passed its first ten settings/preview/diagnostics checks. Its keyboard retest could not establish foreground ownership for the fixture (`AttachThreadInput` returned false and foreground remained elsewhere). The harness now aborts before injecting input when that prerequisite fails, and checks foreground before each test key. Keyboard verification for that final packaged run is blocked by the desktop's foreground restrictions; it is not reported as a passing run. No further retries were made after the guarded failure.
- Tests did not save user settings, enable startup, close user documents or leave a new production instance running. Physical-keyboard use, unsaved-document prompts, elevated applications, IME composition, and hardware hotplug are not covered by these automated checks. Search does not support IME/dead-key composition.

## Update — 2026-10-01

- Release build: zero warnings/errors.
- 42 ordering, geometry, aspect-ratio and DPI layout checks passed.
- Real cross-process activation harness: all six checks passed (establish foreground, switch, reverse switch, minimize, restore and activate, cancel without changing focus).
- Desktop smoke: exit 0; four DWM thumbnails registered and displayed. Composed screenshot visually inspected: rounded cards, readable titles, app icons, live previews and selection outline rendered correctly at 150% scale.
- This validates the activation service directly. Physical Alt+Tab key delivery and elevated/protected applications still require interactive testing; the user's exact failing application has not been identified.

## Original prototype

2026-09-29, Windows host, .NET SDK 10.0.401 targeting .NET 8:

- Release build: successful, zero warnings/errors.
- Dependency-free regression runner: all 14 ordering/geometry checks passed.
- Windows x64 self-contained single-file publish: successful.
- Framework-dependent startup smoke: exit 0 (sandbox window enumeration was restricted).
- Self-contained desktop smoke outside the filesystem sandbox: exit 0; keyboard hook and four event-hook subscriptions initialized; four connected display bounds detected, including negative coordinates; three eligible windows found on the mouse display.
- Rendered overlay inspected at 150% scaling: three application icons/titles and selected-row border visible without clipping.

Smoke interception was disabled and no foreground activation was attempted. This does **not** verify physical Alt+Tab gestures, UAC transitions, fullscreen games, Explorer restart, monitor hotplug, Windows 10, ARM64, or all mixed-DPI combinations. Those remain the manual acceptance checks in TEST_PLAN.md. Smoke processes exited; startup was not enabled and no ongoing keyboard interception was left running.
