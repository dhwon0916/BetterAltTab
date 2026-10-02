using System.Runtime.InteropServices;
using System.Text;

namespace BetterAltTab;

internal sealed class WindowService : IDisposable
{
    private readonly Dictionary<nint, string> lastMonitor = new();
    private readonly IVirtualDesktopManager? desktops;
    private bool disposed;
    public MonitorHistory History { get; } = new();
    public WindowService()
    {
        try { desktops = (IVirtualDesktopManager?)Activator.CreateInstance(Type.GetTypeFromCLSID(new Guid("aa509086-5ca9-4c25-8f95-589d3c07b48a"))!); }
        catch (COMException) { }
    }
    public Screen ActiveMonitor(bool focused)
    {
        if (focused) return Screen.AllScreens.FirstOrDefault(s => s.DeviceName == MonitorFor(Native.GetForegroundWindow())) ?? Screen.PrimaryScreen!;
        Native.GetCursorPos(out var point);
        return Screen.FromPoint(point);
    }
    public string MonitorFor(nint hwnd)
    {
        if (Native.IsIconic(hwnd) && lastMonitor.TryGetValue(hwnd, out var saved) && Screen.AllScreens.Any(s => s.DeviceName == saved)) return saved;
        if (Native.IsIconic(hwnd)) return Screen.FromHandle(hwnd).DeviceName;
        if (Native.DwmGetBounds(hwnd, 9, out var bounds, Marshal.SizeOf<Native.Rect>()) != 0) Native.GetWindowRect(hwnd, out bounds);
        var screens = Screen.AllScreens;
        var screen = screens[0];
        long largest = 0;
        foreach (var candidate in screens)
        {
            long area = MonitorHistory.Area(bounds.Rectangle, candidate.Bounds);
            if (area > largest) { largest = area; screen = candidate; }
        }
        if (largest == 0) screen = Screen.FromHandle(hwnd);
        return lastMonitor[hwnd] = screen.DeviceName;
    }
    public bool Eligible(nint hwnd, bool minimized)
        => ExclusionReason(hwnd, minimized) == null;
    public string? ExclusionReason(nint hwnd, bool minimized)
    {
        if (!Native.IsWindow(hwnd)) return "destroyed";
        if (!Native.IsWindowVisible(hwnd)) return "hidden";
        if (hwnd == Native.GetShellWindow()) return "shell";
        if (!minimized && Native.IsIconic(hwnd)) return "minimized disabled";
        Native.GetWindowThreadProcessId(hwnd, out var pid);
        if (pid == Environment.ProcessId) return "own window";
        var style = Native.GetWindowLong(hwnd, -20);
        if ((style & (0x80 | 0x08000000)) != 0) return "tool/non-activating window";
        if (Native.DwmGetWindowAttribute(hwnd, 14, out var cloaked, 4) == 0 && cloaked != 0) return "DWM cloaked";
        if (desktops != null && desktops.IsWindowOnCurrentVirtualDesktop(hwnd, out var current) == 0 && !current) return "other virtual desktop";
        var cls = new StringBuilder(256); Native.GetClassName(hwnd, cls, cls.Capacity);
        if (cls.ToString() is "Progman" or "WorkerW" or "Shell_TrayWnd" or "Shell_SecondaryTrayWnd") return "shell class";
        if ((style & 0x40000) == 0) // APPWINDOW overrides the owner chain.
        {
            var walk = Native.GetAncestor(hwnd, 3);
            for (var i = 0; i < 32; i++)
            {
                var popup = Native.GetLastActivePopup(walk);
                if (popup == walk || popup == 0) break;
                walk = popup;
                if (Native.IsWindowVisible(walk)) break;
            }
            if (walk != hwnd) return "owned window represented by popup/root";
        }
        return null;
    }
    public List<WindowEntry> Snapshot(string monitor, bool minimized)
    {
        var found = new Dictionary<nint, WindowEntry>();
        var processNames = new Dictionary<uint, string>();
        Native.EnumWindows((hwnd, _) =>
        {
            var reason = ExclusionReason(hwnd, minimized);
            if (reason == null && MonitorFor(hwnd) != monitor) reason = "other monitor";
            if (reason == null)
            {
                var title = new StringBuilder(1024); Native.GetWindowText(hwnd, title, title.Capacity);
                Native.GetWindowThreadProcessId(hwnd, out var pid);
                if (!processNames.TryGetValue(pid, out var processName)) processNames[pid] = processName = ProcessIdentity.ForProcess(pid);
                found[hwnd] = new(hwnd, title.Length == 0 ? "Untitled window" : title.ToString(), monitor, processName);
            }
            if (Diagnostics.Enabled) Diagnostics.Write($"window=0x{hwnd:X} {(reason == null ? "included" : "excluded: " + reason)}");
            return true;
        }, 0);
        foreach (var hwnd in lastMonitor.Keys.Where(h => !Native.IsWindow(h)).ToArray()) Remove(hwnd);
        return History.Order(monitor, found.Keys).Select(h => found[h]).ToList();
    }
    public void SeedPlacement() => Native.EnumWindows((hwnd, _) => { Track(hwnd, false); return true; }, 0);
    public void Track(nint hwnd, bool foreground)
    {
        if (!Eligible(hwnd, true)) return;
        var monitor = MonitorFor(hwnd);
        if (foreground) History.Touch(monitor, hwnd);
    }
    public void Remove(nint hwnd) { lastMonitor.Remove(hwnd); History.Remove(hwnd); }
    public void ResetDisplays()
    {
        var connected = Screen.AllScreens.Select(s => s.DeviceName).ToHashSet();
        // Retain minimized placement on displays whose identity survived the change.
        foreach (var hwnd in lastMonitor.Where(pair => !connected.Contains(pair.Value)).Select(pair => pair.Key).ToArray()) lastMonitor.Remove(hwnd);
    }
    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        if (desktops != null) Marshal.ReleaseComObject(desktops);
    }
}
