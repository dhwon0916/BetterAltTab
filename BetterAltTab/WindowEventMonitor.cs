namespace BetterAltTab;

// WinEvent callbacks run on the installing UI thread. Coalesce noisy location
// events while preserving the chronological order of foreground changes.
internal sealed class WindowEventMonitor : IDisposable
{
    private readonly Native.EventProc callback;
    private readonly List<nint> hooks = new();
    private readonly DeferredSet<nint> pendingLocations = new();
    private readonly Action<Action> post;
    private readonly Action<nint, bool> track;
    private readonly Action<nint> remove;
    private readonly Action desktopChanged;
    private bool disposed;
    public int HookCount => hooks.Count;

    public WindowEventMonitor(Action<Action> post, Action<nint, bool> track, Action<nint> remove, Action desktopChanged)
    {
        this.post = post;
        this.track = track;
        this.remove = remove;
        this.desktopChanged = desktopChanged;
        callback = OnEvent;
        try
        {
            foreach (uint evt in new uint[] { 3, 0x8001, 0x800B, 0x20 })
            {
                var handle = Native.SetWinEventHook(evt, evt, 0, callback, 0, 0, 2);
                if (handle == 0) throw new InvalidOperationException("Could not subscribe to window events.");
                hooks.Add(handle);
            }
        }
        catch { Dispose(); throw; }
    }

    private void OnEvent(nint hook, uint evt, nint hwnd, int obj, int child, uint thread, uint time)
    {
        if (disposed) return;
        if (evt == 0x20) { Dispatch(desktopChanged); return; }
        if (hwnd == 0 || obj != 0 || child != 0) return;
        if (evt == 0x800B)
        {
            if (!pendingLocations.Add(hwnd)) return;
            Dispatch(() =>
            {
                foreach (var window in pendingLocations.Drain()) track(window, false);
            });
        }
        else if (evt == 0x8001)
        {
            pendingLocations.Remove(hwnd);
            Dispatch(() => remove(hwnd));
        }
        else Dispatch(() => track(hwnd, true));
    }

    private void Dispatch(Action action) => post(() => { if (!disposed) action(); });

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        foreach (var hook in hooks) Native.UnhookWinEvent(hook);
        hooks.Clear();
        pendingLocations.Clear();
    }
}
