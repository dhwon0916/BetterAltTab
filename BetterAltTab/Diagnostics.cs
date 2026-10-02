namespace BetterAltTab;

internal static class Diagnostics
{
    private static readonly Queue<string> lines = new();
    public static volatile bool Enabled;
    public static void Write(string message)
    {
        if (!Enabled) return;
        lock (lines)
        {
            lines.Enqueue($"{DateTimeOffset.Now:O} {message}");
            while (lines.Count > 1000) lines.Dequeue();
        }
    }
    public static string Snapshot() { lock (lines) return string.Join(Environment.NewLine, lines); }
    public static void Clear() { lock (lines) lines.Clear(); }
    public static void ShowWindow() => new DiagnosticsForm().Show();
}
