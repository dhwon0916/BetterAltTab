using System.Diagnostics;

namespace BetterAltTab;

internal static class LifecycleCheck
{
    public static void Run()
    {
        using var context = new ApplicationContext();
        using var dispatcher = new Control(); _ = dispatcher.Handle;
        dispatcher.BeginInvoke(async () =>
        {
            var results = new List<string>();
            void Check(bool pass, string name) { results.Add((pass ? "PASS " : "FAIL ") + name); if (!pass) Environment.ExitCode = 1; }
            try
            {
                for (int i = 0; i < 20; i++)
                {
                    var hook = new KeyboardHook(_ => { }, enabled: false);
                    hook.Dispose(); hook.Dispose();
                    Check(!hook.IsRunning, "Hook thread shuts down cleanly, iteration " + (i + 1));
                }
                for (int i = 0; i < 10; i++)
                {
                    var monitor = new WindowEventMonitor(action => dispatcher.BeginInvoke(action), (_, _) => { }, _ => { }, () => { });
                    Check(monitor.HookCount == 4, "Event subscriptions installed, iteration " + (i + 1));
                    monitor.Dispose(); monitor.Dispose();
                    Check(monitor.HookCount == 0, "Event subscriptions released, iteration " + (i + 1));
                }
                using var windows = new WindowService();
                windows.SeedPlacement();
                var screen = windows.ActiveMonitor(false);
                var entries = windows.Snapshot(screen.DeviceName, true);
                if (entries.Count == 0) entries.Add(new(0, "Fixture", screen.DeviceName));
                uint before = Native.GetGuiResources(Native.GetCurrentProcess(), 0);
                for (int i = 0; i < 10; i++)
                {
                    var overlay = new SwitcherOverlay();
                    overlay.Present(entries, 0, screen);
                    int updates = overlay.PreviewUpdateCount;
                    for (int repeat = 0; repeat < 20; repeat++) overlay.Present(entries, 0, screen);
                    Check(overlay.PreviewUpdateCount == updates, "Selection-only refresh reuses thumbnail geometry, iteration " + (i + 1));
                    overlay.Dismiss();
                    Check(overlay.PreviewCount == 0, "Thumbnail references cleared, iteration " + (i + 1));
                    overlay.Dispose(); overlay.Dispose();
                }
                windows.Dispose(); windows.Dispose();
                uint after = Native.GetGuiResources(Native.GetCurrentProcess(), 0);
                results.Add($"INFO GDI objects before overlay cycles={before}, after={after}; framework caches may retain a small fixed amount");
                Check(after <= before + 20, "Repeated overlays do not accumulate GDI resources");
                using var idleHook = new KeyboardHook(_ => { }, enabled: false);
                using var process = Process.GetCurrentProcess();
                var cpu = process.TotalProcessorTime;
                var watch = Stopwatch.StartNew(); await Task.Delay(1000);
                process.Refresh();
                results.Add($"INFO Disabled-hook idle sample: {(process.TotalProcessorTime - cpu).TotalMilliseconds:F1} ms CPU over {watch.Elapsed.TotalMilliseconds:F0} ms wall time (not a production benchmark)");
            }
            catch (Exception ex) { results.Add("FAIL " + ex); Environment.ExitCode = 1; }
            finally { Directory.CreateDirectory("artifacts"); File.WriteAllLines("artifacts/lifecycle-test.txt", results); context.ExitThread(); }
        });
        Application.Run(context);
    }
}
