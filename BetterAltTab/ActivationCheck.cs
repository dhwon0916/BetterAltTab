using System.Diagnostics;

namespace BetterAltTab;

// Opt-in integration harness: two disposable windows in separate processes.
// Exercises real foreground ownership, not just the SetForegroundWindow return value.
internal static class ActivationCheck
{
    public static void Run(string[] args)
    {
        if (args.Contains("--test-window"))
        {
            using var form = new Form { Text = "BetterAltTab activation fixture " + args.Last(), Size = new Size(480, 260), StartPosition = FormStartPosition.CenterScreen };
            form.Controls.Add(new Label { Text = "Temporary activation test window", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter });
            form.Shown += (_, _) => form.BeginInvoke(() => { Native.ShowWindowAsync(form.Handle, 5); Native.SetForegroundWindow(form.Handle); });
            using var timer = new System.Windows.Forms.Timer { Interval = 20000 };
            timer.Tick += (_, _) => form.Close(); timer.Start(); Application.Run(form); return;
        }
        using var context = new ApplicationContext();
        using var dispatch = new Control(); _ = dispatch.Handle;
        dispatch.BeginInvoke(async () =>
        {
            var results = new List<string>();
            var original = Native.GetForegroundWindow();
            Process? a = null, b = null;
            try
            {
                a = Start("A"); var first = await Window(a);
                b = Start("B"); var second = await Window(b);
                await Task.Delay(150);
                Check(await WindowActivation.ActivateAsync(second, CancellationToken.None) && Native.GetForegroundWindow() == second, "Establish fixture B as foreground", results);
                Check(await WindowActivation.ActivateAsync(first, CancellationToken.None) && Native.GetForegroundWindow() == first, "Activate a different process", results);
                Check(await WindowActivation.ActivateAsync(second, CancellationToken.None) && Native.GetForegroundWindow() == second, "Switch back to previous process", results);
                Native.ShowWindowAsync(first, 6); await Task.Delay(100);
                Check(Native.IsIconic(first), "Fixture A minimized", results);
                Check(await WindowActivation.ActivateAsync(first, CancellationToken.None) && !Native.IsIconic(first) && Native.GetForegroundWindow() == first, "Restore and activate minimized process", results);
                using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
                Check(!await WindowActivation.ActivateAsync(second, cancelled.Token) && Native.GetForegroundWindow() == first, "Cancelled switch does not change focus", results);
                Check(ProcessIdentity.Name(second) == "betteralttab", "Foreground app name resolves for exclusion rules", results);
                Check(Native.PostMessage(second, 0x112, 0xF060, 0), "Normal close command posted to fixture", results);
                for (int i = 0; i < 25 && Native.IsWindow(second); i++) await Task.Delay(20);
                Check(!Native.IsWindow(second), "Fixture closes through normal window protocol", results);
            }
            catch (Exception ex) { results.Add("FAIL " + ex); Environment.ExitCode = 1; }
            finally
            {
                foreach (var process in new[] { a, b })
                {
                    if (process == null) continue;
                    if (!process.HasExited) process.CloseMainWindow();
                    process.Dispose();
                }
                if (Native.IsWindow(original)) Native.SetForegroundWindow(original);
                Directory.CreateDirectory("artifacts"); File.WriteAllLines("artifacts/activation-test.txt", results);
                context.ExitThread();
            }
        });
        Application.Run(context);
    }
    private static Process Start(string name) => Process.Start(new ProcessStartInfo(Environment.ProcessPath!, "--test-window " + name) { UseShellExecute = false, WindowStyle = ProcessWindowStyle.Hidden })!;
    private static async Task<nint> Window(Process process)
    {
        for (int i = 0; i < 100; i++)
        {
            process.Refresh(); if (process.MainWindowHandle != 0) return process.MainWindowHandle;
            await Task.Delay(20);
        }
        throw new InvalidOperationException("Fixture did not create a window.");
    }
    private static void Check(bool pass, string name, List<string> results)
    { results.Add((pass ? "PASS " : "FAIL ") + name); if (!pass) Environment.ExitCode = 1; }
}
