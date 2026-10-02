using BetterAltTab;
using System.Drawing;

var count = 0;
void Equal<T>(T expected, T actual, string name)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual)) throw new Exception($"{name}: expected {expected}, got {actual}");
    Console.WriteLine($"PASS {name}"); count++;
}
var history = new MonitorHistory();
history.Touch("left", 3); history.Touch("left", 2); history.Touch("left", 1);
history.Touch("right", 4); history.Touch("right", 5); history.Touch("right", 4);
Equal("1,2,3", string.Join(',', history.Order("left", [3, 2, 1])), "Other monitor activity preserves MRU");
Equal("4,5", string.Join(',', history.Order("right", [5, 4])), "Second monitor has independent MRU");
history.Touch("right", 2);
Equal("1,3", string.Join(',', history.Order("left", [1, 3])), "Moved window leaves old list");
Equal("2,4,5", string.Join(',', history.Order("right", [4, 5, 2])), "Moved foreground window leads new list");
Equal("1,3,8", string.Join(',', history.Order("left", [8, 3, 1])), "Unseen windows follow known MRU");
history.Remove(1);
Equal("3,1", string.Join(',', history.Order("left", [1, 3])), "Destroyed handle loses history");
Equal(2, MonitorHistory.Step(0, 3, true), "Reverse wraps");
Equal(0, MonitorHistory.Step(2, 3, false), "Forward wraps");
Equal(0, MonitorHistory.Step(0, 1, false), "One window");
Equal(-1, MonitorHistory.Step(0, 0, false), "No windows");
var window = new Rectangle(-100, 0, 500, 400);
Equal(40000L, MonitorHistory.Area(window, new Rectangle(-1920, 0, 1920, 1080)), "Negative monitor coordinates");
Equal(160000L, MonitorHistory.Area(window, new Rectangle(0, 0, 1920, 1080)), "Spanning window majority");
Equal(0L, MonitorHistory.Area(window, new Rectangle(2000, 0, 1920, 1080)), "Disjoint display");
Equal(10000000000L, MonitorHistory.Area(new Rectangle(0, 0, 100000, 100000), new Rectangle(0, 0, 100000, 100000)), "Area avoids integer overflow");
// Combined user size (50–200%) and display DPI (100–200%).
foreach (var scale in new[] { .5f, .75f, 1f, 1.5f, 2f, 3f, 4f })
{
    foreach (var size in new[] { new Size(1920, 1080), new Size(682, 2560), new Size(2560, 720) })
    {
        var cards = CardLayout.Create(30, 29, size, scale);
        Equal(true, cards.First <= 29 && cards.First + cards.Cards.Count > 29, $"Selection visible {size} @ {scale}");
        Equal(true, cards.Size.Width <= size.Width && cards.Size.Height <= size.Height, $"Overlay fits {size} @ {scale}");
        Equal(true, cards.Cards.All(r => new Rectangle(Point.Empty, cards.Size).Contains(r)), $"Cards contained {size} @ {scale}");
    }
}
Equal(new Rectangle(0, 25, 200, 100), CardLayout.Fit(new Size(1000, 500), new Rectangle(0, 0, 200, 150)), "Preview preserves aspect ratio");
Console.WriteLine($"{count} checks passed.");
var session = new SwitchSession([new(1, "Budget report", "left", "excel"), new(2, "Code editor", "left", "code"), new(3, "Browser", "left", "chrome"), new(4, "Notes", "left", "notepad")], 1, false);
Equal((nint)2, session.Current!.Handle, "Initial session advances from foreground");
session.Move(2); Equal((nint)4, session.Current!.Handle, "Down arrow moves one grid row");
session.Move(-2); Equal((nint)2, session.Current!.Handle, "Up arrow returns to previous row");
session.Search("EXCEL"); Equal((nint)1, session.Current!.Handle, "Case-insensitive app-name search");
session.Search("report"); Equal((nint)1, session.Current!.Handle, "Window-title search");
session.Search("no match"); Equal(true, session.Current == null, "Empty search has no activatable selection");
session.Move(1); Equal(-1, session.Selected, "Navigation on empty search stays safe");
session.Search(""); Equal(4, session.Visible.Count, "Clearing search restores source list");
session.Select(3); session.Remove(4); Equal(3, session.Visible.Count, "Destroyed window removed during gesture");
Equal(true, session.Current != null && session.Current.Handle != 4, "Destroyed selection falls back safely");
session.Search(new string('x', 120)); Equal(100, session.Query.Length, "Search length bounded");
Equal("mstsc,game", string.Join(',', AppRules.Parse(" mstsc.exe\nGAME.EXE;game.exe")), "Exclusions normalize and deduplicate");
Equal(true, AppRules.Matches(@"C:\Games\GAME.EXE", ["game"]), "Exact process name exclusion");
Equal(false, AppRules.Matches("gamehelper.exe", ["game"]), "Exclusions do not overmatch helpers");
Equal(false, AppRules.Matches("", ["game"]), "Missing process identity not a false match");
Console.WriteLine($"{count} checks passed including feature regressions.");
using (var operations = new LatestOperation())
{
    using var first = operations.Begin();
    using var second = operations.Begin();
    Equal(true, first.Token.IsCancellationRequested, "New request cancels earlier operation without disposing its token");
    first.Dispose(); operations.Cancel();
    Equal(true, second.IsCancellationRequested, "Old completion does not clear current operation");
    using var third = operations.Begin();
    operations.Dispose();
    Equal(true, third.IsCancellationRequested, "Owner shutdown cancels pending work");
    bool rejected = false;
    try { operations.Begin(); } catch (ObjectDisposedException) { rejected = true; }
    Equal(true, rejected, "Disposed owner rejects new operations");
}
var original = new List<WindowEntry> { new(1, "One", "left"), new(2, "Two", "left") };
var isolated = new SwitchSession(original, 0, false);
isolated.Remove(1);
Equal(2, original.Count, "Session removal does not mutate caller's snapshot");
var visibleBefore = isolated.Visible;
Equal(false, isolated.Remove(999), "Unrelated destroyed window does not alter session");
Equal(true, ReferenceEquals(visibleBefore, isolated.Visible), "Unrelated destruction avoids filtering allocation");
isolated.Search("A😀"); isolated.Backspace();
Equal("A", isolated.Query, "Backspace removes whole surrogate pair");
isolated.Search("Ae\u0301"); isolated.Backspace();
Equal("A", isolated.Query, "Backspace removes combining character as one text element");
isolated.Search(""); isolated.Backspace();
Equal("", isolated.Query, "Backspace on empty query remains safe");
Console.WriteLine($"{count} total regression checks passed.");
var deferred = new DeferredSet<nint>();
int scheduled = 0;
for (int i = 0; i < 10000; i++) if (deferred.Add(i % 3)) scheduled++;
Equal(1, scheduled, "Ten thousand movement events schedule only one drain");
deferred.Remove(1);
Equal("0,2", string.Join(',', deferred.Drain().Order()), "Batch deduplicates windows and drops destroyed handles");
Equal(true, deferred.Add(5), "Next movement burst schedules a new drain");
deferred.Clear();
Equal(0, deferred.Drain().Length, "Shutdown clears pending movement work");
Console.WriteLine($"{count} final checks passed.");
