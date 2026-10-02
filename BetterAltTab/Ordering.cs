namespace BetterAltTab;

// Pure ordering/geometry logic is shared with the dependency-free test runner.
internal sealed class MonitorHistory
{
    private readonly Dictionary<string, List<nint>> lists = new();
    public void Touch(string monitor, nint window)
    {
        foreach (var list in lists.Values) list.Remove(window);
        if (!lists.TryGetValue(monitor, out var target)) lists[monitor] = target = new();
        target.Insert(0, window);
    }
    public void Remove(nint window) { foreach (var list in lists.Values) list.Remove(window); }
    public IEnumerable<nint> Order(string monitor, IEnumerable<nint> windows)
    {
        var remaining = windows.ToList();
        if (lists.TryGetValue(monitor, out var recent))
            foreach (var hwnd in recent) if (remaining.Remove(hwnd)) yield return hwnd;
        foreach (var hwnd in remaining) yield return hwnd;
    }
    public static int Step(int index, int count, bool backward) => count == 0 ? -1 : (index + (backward ? -1 : 1) + count) % count;
    public static long Area(System.Drawing.Rectangle a, System.Drawing.Rectangle b)
    {
        var r = System.Drawing.Rectangle.Intersect(a, b);
        return r.IsEmpty ? 0 : (long)r.Width * r.Height;
    }
}
