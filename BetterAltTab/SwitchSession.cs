namespace BetterAltTab;

internal sealed record WindowEntry(nint Handle, string Title, string Monitor, string AppName = "");

internal sealed class SwitchSession
{
    private readonly List<WindowEntry> source;
    public List<WindowEntry> Visible { get; private set; }
    public int Selected { get; private set; }
    public string Query { get; private set; } = "";
    public WindowEntry? Current => Selected >= 0 && Selected < Visible.Count ? Visible[Selected] : null;
    public SwitchSession(List<WindowEntry> entries, nint foreground, bool backward)
    {
        source = entries.ToList(); Visible = entries.ToList();
        int active = Visible.FindIndex(w => w.Handle == foreground);
        Selected = entries.Count == 0 ? -1 : active >= 0 ? MonitorHistory.Step(active, entries.Count, backward) : backward ? entries.Count - 1 : 0;
    }
    public void Select(int index) { if (index >= 0 && index < Visible.Count) Selected = index; }
    public void Move(int delta) { if (Visible.Count > 0) Selected = ((Selected + delta) % Visible.Count + Visible.Count) % Visible.Count; }
    public void Search(string query)
    {
        var handle = Current?.Handle;
        Query = query.Length > 100 ? query[..100] : query;
        Visible = source.Where(w => w.Title.Contains(Query, StringComparison.OrdinalIgnoreCase) || w.AppName.Contains(Query, StringComparison.OrdinalIgnoreCase)).ToList();
        Selected = Visible.FindIndex(w => w.Handle == handle);
        if (Selected < 0 && Visible.Count > 0) Selected = 0;
    }
    public void Backspace()
    {
        if (Query.Length == 0) return;
        var positions = System.Globalization.StringInfo.ParseCombiningCharacters(Query);
        Search(Query[..positions[^1]]);
    }
    public bool Remove(nint hwnd)
    {
        if (source.RemoveAll(w => w.Handle == hwnd) == 0) return false;
        Search(Query);
        return true;
    }
}

internal static class AppRules
{
    public static string Normalize(string value)
    {
        var name = value.Trim();
        int separator = Math.Max(name.LastIndexOf('\\'), name.LastIndexOf('/'));
        if (separator >= 0) name = name[(separator + 1)..];
        return name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ? name[..^4].ToLowerInvariant() : name.ToLowerInvariant();
    }
    public static string[] Parse(string value) => value.Split([';', ',', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries).Select(Normalize).Where(s => s.Length > 0).Distinct().ToArray();
    public static bool Matches(string process, IEnumerable<string> rules) => rules.Contains(Normalize(process), StringComparer.OrdinalIgnoreCase);
}
