namespace BetterAltTab;

internal static class WindowActivation
{
    public static async Task<bool> ActivateAsync(nint hwnd, CancellationToken cancellation)
    {
        Diagnostics.Write($"Activation requested window=0x{hwnd:X}");
        if (!Native.IsWindow(hwnd) || cancellation.IsCancellationRequested) { Diagnostics.Write("Activation abandoned: destroyed window or cancelled request"); return false; }
        var previous = Native.GetForegroundWindow();
        if (Native.IsIconic(hwnd))
        {
            Native.ShowWindowAsync(hwnd, 9);
            // Restore is asynchronous across processes. Do not race it with activation.
            for (var i = 0; i < 10 && Native.IsIconic(hwnd); i++)
                await Task.Delay(20, cancellation);
        }
        cancellation.ThrowIfCancellationRequested();
        if (!Native.IsWindow(hwnd)) { Diagnostics.Write("Activation abandoned: window destroyed during restore"); return false; }
        var foreground = Native.GetForegroundWindow();
        if (foreground != previous && foreground != hwnd) { Diagnostics.Write("Activation cancelled: foreground changed during restore"); return false; }
        Native.SetForegroundWindow(hwnd);
        await Task.Delay(40, cancellation);
        if (Native.GetForegroundWindow() == hwnd) { Diagnostics.Write("Activation succeeded on initial request"); return true; }
        if (Native.GetForegroundWindow() != previous) { Diagnostics.Write("Activation cancelled: another window received focus"); return false; }

        // A background tray thread does not share the foreground input queue. Join
        // only for this user-requested switch, never across an await or a hung app.
        var currentThread = Native.GetCurrentThreadId();
        var foregroundThread = Native.GetWindowThreadProcessId(previous, out _);
        bool attached = false;
        try
        {
            if (!Native.IsHungAppWindow(previous) && !Native.IsHungAppWindow(hwnd) && foregroundThread != 0 && foregroundThread != currentThread)
                attached = Native.AttachThreadInput(currentThread, foregroundThread, true);
            Diagnostics.Write($"Activation retry: foreground queue attached={attached}");
            Native.BringWindowToTop(hwnd);
            Native.SetForegroundWindow(hwnd);
        }
        finally { if (attached) Native.AttachThreadInput(currentThread, foregroundThread, false); }
        await Task.Delay(60, cancellation);
        var activated = Native.GetForegroundWindow() == hwnd;
        Diagnostics.Write($"Activation {(activated ? "succeeded" : "failed")}; foreground=0x{Native.GetForegroundWindow():X}; target=0x{hwnd:X}. A failed request can indicate foreground-lock or integrity restrictions.");
        return activated;
    }
}
