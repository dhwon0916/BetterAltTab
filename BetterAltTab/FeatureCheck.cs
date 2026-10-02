using System.Text.Json;

namespace BetterAltTab;

internal static class FeatureCheck
{
    public static void Run()
    {
        using var context = new ApplicationContext();
        using var dispatcher = new Control(); _ = dispatcher.Handle;
        dispatcher.BeginInvoke(async () =>
        {
            var results = new List<string>();
            var original = Native.GetForegroundWindow();
            try
            {
                void Check(bool pass, string name)
                {
                    results.Add((pass ? "PASS " : "FAIL ") + name);
                    if (!pass) Environment.ExitCode = 1;
                }
                var configured = new Settings { OverlayScalePercent = 110, IncludeMinimized = false, ExcludedApps = ["game", "mstsc"], Displays = new() { ["display2"] = new(160, true) } };
                var roundtrip = JsonSerializer.Deserialize<Settings>(JsonSerializer.Serialize(configured))!;
                Check(roundtrip.ForDisplay("display2") == new MonitorPreferences(160, true), "Per-display settings round trip");
                Check(roundtrip.ForDisplay("other") == new MonitorPreferences(110, false), "Other displays inherit defaults");
                Check(roundtrip.ExcludedApps.SequenceEqual(configured.ExcludedApps), "Exclusions round trip");
                Check(JsonSerializer.Deserialize<Settings>("{}")!.ForDisplay("new") == new MonitorPreferences(100, true), "Old settings retain defaults");
                Diagnostics.Enabled = true;
                for (int i = 0; i < 1100; i++) Diagnostics.Write("test event " + i);
                Check(Diagnostics.Snapshot().Split(Environment.NewLine).Length == 1000, "Diagnostics buffer bounded");
                Diagnostics.Enabled = false;
                var before = Diagnostics.Snapshot(); Diagnostics.Write("should not record");
                Check(before == Diagnostics.Snapshot(), "Disabled diagnostics do not record");
                Diagnostics.Clear();
                using var form = new SettingsForm(configured, _ => throw new InvalidOperationException("Tests must not save user settings"));
                form.TopMost = true; form.Show(); Native.ShowWindowAsync(form.Handle, 5); await Task.Delay(100);
                var controls = Descendants(form).ToList();
                var scale = controls.OfType<NumericUpDown>().Single();
                Check(scale.Value == 110, "Settings form loads default size");
                scale.Value = 150;
                await Task.Delay(200);
                var preview = Application.OpenForms.OfType<SwitcherOverlay>().FirstOrDefault(f => f.Visible);
                Check(preview != null, "Changing size opens live preview");
                if (preview != null)
                {
                    Check(!preview.Bounds.IntersectsWith(form.Bounds), "Live preview leaves settings controls accessible");
                    using var capture = new Bitmap(preview.Width, preview.Height);
                    using var graphics = Graphics.FromImage(capture);
                    graphics.CopyFromScreen(preview.Location, Point.Empty, capture.Size);
                    Directory.CreateDirectory("artifacts"); capture.Save("artifacts/live-size-preview.png");
                }
                using (var capture = new Bitmap(form.Width, form.Height))
                { form.DrawToBitmap(capture, new Rectangle(Point.Empty, capture.Size)); Directory.CreateDirectory("artifacts"); capture.Save("artifacts/settings-features.png"); }
                form.Close();
                Check(!Application.OpenForms.OfType<SwitcherOverlay>().Any(f => f.Visible), "Closing settings disposes preview");
                using var receiver = new Form { Text = "BetterAltTab keyboard test", Size = new Size(360, 180), TopMost = true, StartPosition = FormStartPosition.CenterScreen };
                receiver.Show(); Native.ShowWindowAsync(receiver.Handle, 5);
                await Task.Delay(100);
                Diagnostics.Enabled = true;
                var receiverReady = await WindowActivation.ActivateAsync(receiver.Handle, CancellationToken.None);
                Check(receiverReady, "Keyboard test receiver activated");
                if (!receiverReady)
                    throw new InvalidOperationException("Keyboard test stopped before injecting input: foreground receiver unavailable. " + Diagnostics.Snapshot());
                Diagnostics.Enabled = false;
                var actions = new List<SwitchAction>(); var typed = new List<string>();
                using (var hook = new KeyboardHook(action => dispatcher.BeginInvoke(() => actions.Add(action)), true))
                {
                    hook.TextEntered = text => dispatcher.BeginInvoke(() => typed.Add(text));
                    async Task Key(Keys key, bool up = false)
                    {
                        if (!up && Native.GetForegroundWindow() != receiver.Handle)
                            throw new InvalidOperationException("Keyboard test stopped: foreground changed away from its receiver.");
                        var input = new Native.Input { Type = 1, Keyboard = new Native.KeyboardInput { Key = (ushort)key, Flags = up ? 2u : 0u, Extra = KeyboardHook.TestInputTag } };
                        if (Native.SendInput(1, [input], System.Runtime.InteropServices.Marshal.SizeOf<Native.Input>()) != 1) throw new InvalidOperationException("Test key injection rejected");
                        await Task.Delay(25);
                    }
                    async Task Tap(Keys key) { await Key(key); await Key(key, true); }
                    try
                    {
                        await Key(Keys.LMenu); await Tap(Keys.Tab); await Tap(Keys.Right); await Tap(Keys.Down); await Tap(Keys.Up); await Tap(Keys.Left); await Tap(Keys.A); await Tap(Keys.Back); await Tap(Keys.Enter); await Key(Keys.LMenu, true);
                        Check(actions.SequenceEqual(new[] { SwitchAction.Forward, SwitchAction.Forward, SwitchAction.Down, SwitchAction.Up, SwitchAction.Backward, SwitchAction.Backspace, SwitchAction.Commit }), "Real hook routes navigation and Enter once");
                        Check(string.Concat(typed).Equals("a", StringComparison.OrdinalIgnoreCase), "Real hook decodes search text");
                        actions.Clear(); typed.Clear();
                        await Key(Keys.LMenu); await Tap(Keys.Tab); await Key(Keys.LMenu, true);
                        Check(actions.SequenceEqual(new[] { SwitchAction.Forward, SwitchAction.Commit }), "Alt release still commits");
                        actions.Clear();
                        await Key(Keys.LMenu); await Tap(Keys.Tab); await Tap(Keys.Escape); await Key(Keys.LMenu, true);
                        Check(actions.SequenceEqual(new[] { SwitchAction.Forward, SwitchAction.Cancel }), "Escape cancels without later commit");
                        actions.Clear(); hook.CycleKey = Keys.F12; hook.ExcludedApps = ["betteralttab"];
                        await Key(Keys.LMenu); await Tap(Keys.F12); hook.ExcludedApps = []; await Tap(Keys.F12); await Key(Keys.LMenu, true);
                        Check(actions.Count == 0, "Exclusion bypass stays latched until Alt release");
                        await Key(Keys.LMenu); await Tap(Keys.F12); await Key(Keys.LMenu, true);
                        Check(actions.SequenceEqual(new[] { SwitchAction.Forward, SwitchAction.Commit }), "Next gesture resumes custom switching");
                    }
                    finally { await Key(Keys.LMenu, true); }
                }
                receiver.Close();
            }
            catch (Exception ex) { results.Add("FAIL " + ex); Environment.ExitCode = 1; }
            finally
            {
                if (Native.IsWindow(original)) Native.SetForegroundWindow(original);
                Directory.CreateDirectory("artifacts"); File.WriteAllLines("artifacts/feature-test.txt", results); context.ExitThread();
            }
        });
        Application.Run(context);
    }
    private static IEnumerable<Control> Descendants(Control root)
    {
        foreach (Control child in root.Controls) { yield return child; foreach (var nested in Descendants(child)) yield return nested; }
    }
}
