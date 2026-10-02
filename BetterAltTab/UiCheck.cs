namespace BetterAltTab;

internal static class UiCheck
{
    public static void Run()
    {
        using var context = new ApplicationContext(); using var dispatcher = new Control(); _ = dispatcher.Handle;
        dispatcher.BeginInvoke(async () =>
        {
            var checks = new List<string>();
            try
            {
                Directory.CreateDirectory("artifacts/ui");
                using var form = new SettingsForm(new Settings { ExcludedApps = ["mstsc", "example-game"] }, _ => throw new InvalidOperationException("Render test cannot save"));
                form.Show(); await Task.Delay(100);
                Capture(form, "settings-general");
                var buttons = Descendants(form).OfType<Button>().ToList();
                foreach (var page in new[] { ("Displays & size", "settings-displays"), ("App exclusions", "settings-apps") })
                {
                    buttons.Single(b => b.Text == page.Item1).PerformClick(); await Task.Delay(50); Capture(form, page.Item2);
                    checks.Add("PASS Settings page renders: " + page.Item1);
                }
                buttons.Single(b => b.Text == "Displays & size").PerformClick();
                var controls = Descendants(form).ToList();
                var spinner = controls.OfType<NumericUpDown>().Single();
                var slider = controls.OfType<TrackBar>().Single();
                controls.OfType<CheckBox>().Single(c => c.Text.StartsWith("Show a live preview")).Checked = false;
                slider.Value = 125;
                checks.Add(spinner.Value == 125 ? "PASS Size slider updates exact percentage" : "FAIL Size slider sync");
                spinner.Value = 140;
                checks.Add(slider.Value == 140 ? "PASS Exact percentage updates slider" : "FAIL Percentage sync");
                var selector = controls.OfType<ComboBox>().Single(c => c.Items.Count > 0 && c.Items[0]!.ToString()!.StartsWith("All displays"));
                if (selector.Items.Count > 1)
                {
                    selector.SelectedIndex = 1;
                    var customize = controls.OfType<CheckBox>().Single(c => c.Text.StartsWith("Override defaults"));
                    checks.Add(customize.Visible && !spinner.Enabled ? "PASS Display initially inherits defaults" : "FAIL Display inheritance state");
                    customize.Checked = true; spinner.Value = 170; selector.SelectedIndex = 0;
                    checks.Add(spinner.Value == 140 ? "PASS Display override leaves global size intact" : "FAIL Override leaked into defaults");
                    selector.SelectedIndex = 1;
                    checks.Add(spinner.Value == 170 ? "PASS Display override remains in draft" : "FAIL Display draft lost");
                }
                checks.Add(buttons.Single(b => b.Text == "Save changes").Visible ? "PASS Save stays visible on all pages" : "FAIL Save is hidden");
                form.Close();
                using (var diagnostics = new DiagnosticsForm())
                { diagnostics.Show(); await Task.Delay(50); Capture(diagnostics, "diagnostics-empty"); diagnostics.Close(); }
                Diagnostics.Enabled = true;
                Diagnostics.Write("Switch started on Display 1; candidates=4; scale=100");
                Diagnostics.Write("window=0x1234 excluded: minimized disabled");
                Diagnostics.Write("Activation succeeded on initial request");
                using (var diagnostics = new DiagnosticsForm())
                { diagnostics.Show(); await Task.Delay(50); Capture(diagnostics, "diagnostics-events"); diagnostics.Close(); }
                Diagnostics.Enabled = false; Diagnostics.Clear(); checks.Add("PASS Diagnostics empty and recorded states render");
                using var service = new WindowService(); using var overlay = new SwitcherOverlay();
                var screen = service.ActiveMonitor(false); var entries = service.Snapshot(screen.DeviceName, true);
                if (entries.Count == 0) entries.Add(new(0, "Example application", screen.DeviceName));
                overlay.Present(entries, 0, screen); Native.ShowWindowAsync(overlay.Handle, 4); await Task.Delay(150);
                using (var bitmap = new Bitmap(overlay.Width, overlay.Height))
                { overlay.DrawToBitmap(bitmap, new Rectangle(Point.Empty, bitmap.Size)); bitmap.Save("artifacts/ui/switcher.png"); }
                checks.Add("PASS Switcher layout rendered; " + overlay.PreviewCount + " DWM thumbnails registered (not captured by DrawToBitmap)"); overlay.Dismiss();
            }
            catch (Exception ex) { checks.Add("FAIL " + ex); Environment.ExitCode = 1; }
            finally { if (checks.Any(c => c.StartsWith("FAIL"))) Environment.ExitCode = 1; File.WriteAllLines("artifacts/ui/checks.txt", checks); context.ExitThread(); }
        });
        Application.Run(context);
    }
    private static void Capture(Form form, string name)
    {
        using var bitmap = new Bitmap(form.Width, form.Height);
        form.DrawToBitmap(bitmap, new Rectangle(Point.Empty, bitmap.Size)); bitmap.Save("artifacts/ui/" + name + ".png");
    }
    private static IEnumerable<Control> Descendants(Control root)
    {
        foreach (Control child in root.Controls) { yield return child; foreach (var nested in Descendants(child)) yield return nested; }
    }
}
