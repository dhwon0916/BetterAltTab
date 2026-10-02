using Microsoft.Win32;

namespace BetterAltTab;

internal sealed class TrayApplication : ApplicationContext
{
    private readonly Control dispatcher = new();
    private readonly WindowService windows = new();
    private readonly SwitcherOverlay overlay = new();
    private readonly NotifyIcon tray = new();
    private readonly KeyboardHook keyboard;
    private readonly WindowEventMonitor? eventMonitor;
    private Settings settings = Settings.Load();
    private SettingsForm? settingsForm;
    private SwitchSession? session;
    private Screen? monitor;
    private MonitorPreferences sessionPreferences = new();
    private readonly LatestOperation activation = new();
    private volatile bool disposed;
    private readonly System.Windows.Forms.Timer? smokeTimer;

    public TrayApplication(bool smokeTest)
    {
        _ = dispatcher.Handle;
        try
        {
            keyboard = new KeyboardHook(action => Post(() => HandleSwitch(action)), enabled: !smokeTest) { CycleKey = settings.CycleKey, ExcludedApps = settings.ExcludedApps };
            keyboard.TextEntered = text => Post(() => { if (session != null) { session.Search(session.Query + text); PresentSession(); } });
            overlay.WindowClicked += index => { session?.Select(index); keyboard.Cancel(); HandleSwitch(SwitchAction.Commit); };
            overlay.CloseClicked += CloseWindow;
            var menu = new ContextMenuStrip();
            var enabled = new ToolStripMenuItem("Enabled") { Checked = true, CheckOnClick = true };
            enabled.CheckedChanged += (_, _) => { keyboard.Enabled = enabled.Checked; Cancel(); tray!.Text = enabled.Checked ? "BetterAltTab" : "BetterAltTab (paused)"; };
            menu.Items.Add(enabled);
            menu.Items.Add("Settings…", null, (_, _) => OpenSettings());
            menu.Items.Add("Troubleshooting…", null, (_, _) => { Cancel(); Diagnostics.ShowWindow(); });
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("Exit", null, (_, _) => ExitThread());
            tray.Icon = AppIcon.Value; tray.Text = "BetterAltTab"; tray.ContextMenuStrip = menu; tray.Visible = true;
            tray.DoubleClick += (_, _) => OpenSettings();
            eventMonitor = new WindowEventMonitor(Post, TrackWindow, RemoveWindow, Cancel);
            SystemEvents.DisplaySettingsChanged += DisplaysChanged;
            SystemEvents.SessionSwitch += SessionChanged;
            // Seed bounds for windows minimized later; no background polling.
            windows.SeedPlacement();
            windows.Track(Native.GetForegroundWindow(), true);
            if (smokeTest)
            {
                var screen = windows.ActiveMonitor(false);
                var preferences = settings.ForDisplay(screen.DeviceName);
                var entries = windows.Snapshot(screen.DeviceName, preferences.IncludeMinimized);
                Directory.CreateDirectory("artifacts");
                File.WriteAllText("artifacts/smoke-test.json", System.Text.Json.JsonSerializer.Serialize(new
                {
                    HookInstalled = true,
                    EventHooks = eventMonitor.HookCount,
                    Displays = Screen.AllScreens.Select(s => new { s.DeviceName, s.Bounds }),
                    ActiveDisplay = screen.DeviceName,
                    EligibleWindowCount = entries.Count
                }, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
                if (entries.Count > 0)
                {
                    overlay.Present(entries, 0, screen, preferences.ScalePercent);
                }
                smokeTimer = new() { Interval = 1200 };
                smokeTimer.Tick += (_, _) =>
                {
                    smokeTimer.Stop();
                    if (overlay.Visible)
                    {
                        // DWM previews are composed outside WinForms DrawToBitmap.
                        using var bitmap = new Bitmap(overlay.Width, overlay.Height);
                        using var graphics = Graphics.FromImage(bitmap);
                        graphics.CopyFromScreen(overlay.Location, Point.Empty, bitmap.Size);
                        bitmap.Save("artifacts/switcher-preview.png");
                        File.WriteAllText("artifacts/thumbnail-count.txt", overlay.PreviewCount.ToString());
                    }
                    ExitThread();
                }; smokeTimer.Start();
            }
        }
        catch { Dispose(); throw; }
    }
    private void Post(Action action)
    {
        if (disposed) return;
        try { dispatcher.BeginInvoke(() => { if (!disposed) action(); }); }
        catch (InvalidOperationException) when (disposed || dispatcher.IsDisposed) { }
    }
    private void TrackWindow(nint hwnd, bool foreground)
    {
        windows.Track(hwnd, foreground);
        if (!foreground) overlay.RefreshSource(hwnd);
        if (foreground && session != null && settings.ExcludedApps.Length > 0 && AppRules.Matches(ProcessIdentity.Name(hwnd), settings.ExcludedApps)) Cancel();
    }
    private void RemoveWindow(nint hwnd)
    {
        windows.Remove(hwnd);
        if (session?.Remove(hwnd) == true) PresentSession();
    }
    private void HandleSwitch(SwitchAction action)
    {
        if (action == SwitchAction.Cancel) { Cancel(); return; }
        if (action == SwitchAction.Commit)
        {
            var chosen = session?.Current;
            overlay.Dismiss(); session = null;
            if (chosen != null && windows.Eligible(chosen.Handle, sessionPreferences.IncludeMinimized) && windows.MonitorFor(chosen.Handle) == monitor?.DeviceName)
            {
                CommitWindow(chosen);
            }
            return;
        }
        if (!keyboard.Enabled) return;
        var backward = action == SwitchAction.Backward;
        if (session == null)
        {
            activation.Cancel();
            monitor = windows.ActiveMonitor(settings.FocusedMonitor);
            sessionPreferences = settings.ForDisplay(monitor.DeviceName);
            var foreground = Native.GetForegroundWindow();
            windows.Track(foreground, true);
            if (action != SwitchAction.Forward && action != SwitchAction.Backward) return;
            session = new SwitchSession(windows.Snapshot(monitor.DeviceName, sessionPreferences.IncludeMinimized), foreground, backward);
            Diagnostics.Write($"Switch started on {monitor.DeviceName}; candidates={session.Visible.Count}; scale={sessionPreferences.ScalePercent}");
        }
        else if (action == SwitchAction.Backspace)
            session.Backspace();
        else session.Move(action switch { SwitchAction.Up => -overlay.Columns, SwitchAction.Down => overlay.Columns, SwitchAction.Backward => -1, _ => 1 });
        PresentSession();
    }
    private void PresentSession()
    {
        if (session == null || monitor == null || !settings.ShowOverlay) return;
        overlay.Present(session.Visible, session.Selected, monitor, sessionPreferences.ScalePercent, session.Query.Length > 0 ? "Search: " + session.Query : "");
    }
    private async void CloseWindow(int index)
    {
        session?.Select(index); var chosen = session?.Current;
        Cancel();
        if (chosen == null || !Native.IsWindow(chosen.Handle)) return;
        using var pending = activation.Begin();
        try
        {
            await WindowActivation.ActivateAsync(chosen.Handle, pending.Token);
            pending.Token.ThrowIfCancellationRequested();
            bool posted = Native.PostMessage(chosen.Handle, 0x112, 0xF060, 0); // WM_SYSCOMMAND / SC_CLOSE, never terminate the process.
            Diagnostics.Write($"Close requested window=0x{chosen.Handle:X}; posted={posted}; win32={System.Runtime.InteropServices.Marshal.GetLastWin32Error()}");
            if (!posted) tray.ShowBalloonTip(2000, "Close request blocked", "Windows did not allow this window to be closed.", ToolTipIcon.Info);
        }
        catch (OperationCanceledException) { Diagnostics.Write("Close request cancelled by a newer user action"); }
    }
    private async void CommitWindow(WindowEntry chosen)
    {
        using var pending = activation.Begin();
        try
        {
            if (await WindowActivation.ActivateAsync(chosen.Handle, pending.Token))
                windows.History.Touch(windows.MonitorFor(chosen.Handle), chosen.Handle);
            else if (!pending.IsCancellationRequested && !disposed)
            {
                Native.FlashWindow(chosen.Handle, true);
                tray.ShowBalloonTip(2500, "Window could not be activated", "Windows blocked foreground activation. Elevated or protected apps may require using their taskbar button.", ToolTipIcon.Info);
            }
        }
        catch (OperationCanceledException) { Diagnostics.Write("Activation cancelled by a newer user action"); }
    }
    private void Cancel() { activation.Cancel(); keyboard.Cancel(); session = null; overlay.Dismiss(); }
    private void DisplaysChanged(object? sender, EventArgs e) => Post(() => { Cancel(); windows.ResetDisplays(); });
    private void SessionChanged(object sender, SessionSwitchEventArgs e) => Post(Cancel);
    private void OpenSettings()
    {
        Cancel();
        if (settingsForm is { IsDisposed: false }) { settingsForm.Activate(); return; }
        settingsForm = new SettingsForm(settings, value => { settings = value; keyboard.CycleKey = value.CycleKey; keyboard.ExcludedApps = value.ExcludedApps; });
        settingsForm.Show();
    }
    protected override void ExitThreadCore() { Dispose(); base.ExitThreadCore(); }
    protected override void Dispose(bool disposing)
    {
        if (disposing && !disposed)
        {
            disposed = true;
            activation.Dispose();
            SystemEvents.DisplaySettingsChanged -= DisplaysChanged; SystemEvents.SessionSwitch -= SessionChanged;
            smokeTimer?.Dispose(); keyboard?.Dispose();
            eventMonitor?.Dispose();
            settingsForm?.Dispose(); overlay.Dispose(); tray.Visible = false; tray.ContextMenuStrip?.Dispose(); tray.Dispose(); windows.Dispose(); dispatcher.Dispose();
        }
        base.Dispose(disposing);
    }
}
