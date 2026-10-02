namespace BetterAltTab;

internal sealed class SettingsForm : Form
{
    private sealed record DisplayChoice(string Title, string Device) { public override string ToString() => Title; }
    private readonly SwitcherOverlay preview = new();
    private readonly WindowService windows = new();
    private readonly System.Windows.Forms.Timer previewTimer = new() { Interval = 2000 };
    private bool changing;
    private string selectedDevice = "";
    private MonitorPreferences defaults;
    private readonly Dictionary<string, MonitorPreferences> displays;
    public SettingsForm(Settings current, Action<Settings> apply)
    {
        defaults = new(current.OverlayScalePercent, current.IncludeMinimized);
        displays = new(current.Displays);
        Text = "BetterAltTab · Settings"; StartPosition = FormStartPosition.CenterScreen;
        Ui.StyleWindow(this); ClientSize = new Size(900, 680);
        MinimumSize = new Size(800, 600); MaximizeBox = false;
        var startup = new CheckBox { Text = "Start with Windows", AutoSize = true, Checked = StartupIntegration.Enabled };
        var focus = new CheckBox { Text = "Follow the focused window instead of the mouse", AutoSize = true, Checked = current.FocusedMonitor };
        var overlay = new CheckBox { Text = "Show switcher overlay", AutoSize = true, Checked = current.ShowOverlay };
        var selector = new ThemedComboBox { Width = 480 };
        selector.Items.Add(new DisplayChoice("All displays · Default preferences", ""));
        int displayNumber = 0;
        foreach (var screen in Screen.AllScreens)
            selector.Items.Add(new DisplayChoice($"Display {++displayNumber}{(screen.Primary ? " · Primary" : "")} · {screen.Bounds.Width} × {screen.Bounds.Height}", screen.DeviceName));
        foreach (var name in displays.Keys.Where(n => !Screen.AllScreens.Any(s => s.DeviceName == n)))
            selector.Items.Add(new DisplayChoice(name + " · Disconnected", name));
        var customize = new CheckBox { Text = "Override defaults for this display", AutoSize = true };
        var minimized = new CheckBox { Text = "Include minimized windows", AutoSize = true };
        var scale = new NumericUpDown { Minimum = 50, Maximum = 200, Increment = 10, Width = 100, AccessibleName = "Switcher size percent" };
        var slider = new TrackBar { Minimum = 50, Maximum = 200, TickStyle = TickStyle.None, SmallChange = 5, LargeChange = 25, AutoSize = false, Height = 32, Width = 420, AccessibleName = "Switcher size slider" };
        var live = new CheckBox { Text = "Show a live preview as I adjust the size", AutoSize = true, Checked = true };
        var previewButton = new Button { Text = "Preview size", AutoSize = true };
        var exclusions = new TextBox { Multiline = true, Width = 480, Height = 170, ScrollBars = ScrollBars.Vertical, BorderStyle = BorderStyle.FixedSingle, Text = string.Join(Environment.NewLine, current.ExcludedApps.Select(s => s + ".exe")), PlaceholderText = "mstsc.exe\r\nyourgame.exe", AccessibleName = "Excluded application executable names" };
        var browse = new Button { Text = "Add application…" };
        browse.Click += (_, _) =>
        {
            using var dialog = new OpenFileDialog { Title = "Choose applications to exclude", Filter = "Applications (*.exe)|*.exe", Multiselect = true };
            if (dialog.ShowDialog(this) != DialogResult.OK) return;
            var names = AppRules.Parse(exclusions.Text).Concat(dialog.FileNames.Select(AppRules.Normalize)).Distinct();
            exclusions.Text = string.Join(Environment.NewLine, names.Select(n => n + ".exe"));
        };
        var key = new ThemedComboBox { Width = 180, DataSource = Settings.AllowedKeys, SelectedItem = current.CycleKey };
        var save = new Button { Text = "Save changes", AutoSize = true };
        var cancel = new Button { Text = "Cancel", AutoSize = true };
        var buttons = new FlowLayoutPanel { Dock = DockStyle.Right, AutoSize = true, FlowDirection = FlowDirection.RightToLeft, WrapContents = false }; buttons.Controls.AddRange([save, cancel]);
        void StoreDisplay()
        {
            var value = new MonitorPreferences((int)scale.Value, minimized.Checked);
            if (selectedDevice.Length == 0) defaults = value;
            else if (customize.Checked) displays[selectedDevice] = value;
            else displays.Remove(selectedDevice);
        }
        void LoadDisplay()
        {
            changing = true;
            bool custom = selectedDevice.Length > 0 && displays.ContainsKey(selectedDevice);
            customize.Visible = selectedDevice.Length > 0; customize.Enabled = selectedDevice.Length > 0; customize.Checked = custom;
            var value = custom ? displays[selectedDevice] : defaults;
            scale.Value = Math.Clamp(value.ScalePercent, 50, 200); slider.Value = (int)scale.Value; minimized.Checked = value.IncludeMinimized;
            scale.Enabled = slider.Enabled = minimized.Enabled = selectedDevice.Length == 0 || custom;
            changing = false;
        }
        void ShowPreview()
        {
            var screen = Screen.AllScreens.FirstOrDefault(s => s.DeviceName == selectedDevice) ?? Screen.FromControl(this);
            var entries = windows.Snapshot(screen.DeviceName, minimized.Checked);
            if (entries.Count == 0) entries.Add(new(0, "Example application", screen.DeviceName));
            var area = screen.WorkingArea;
            // Use the largest free rectangle beside the settings window so the
            // live preview never covers the size control being adjusted.
            var candidates = new[]
            {
                Rectangle.FromLTRB(area.Left, area.Top, Math.Clamp(Left - 16, area.Left, area.Right), area.Bottom),
                Rectangle.FromLTRB(Math.Clamp(Right + 16, area.Left, area.Right), area.Top, area.Right, area.Bottom),
                Rectangle.FromLTRB(area.Left, area.Top, area.Right, Math.Clamp(Top - 16, area.Top, area.Bottom)),
                Rectangle.FromLTRB(area.Left, Math.Clamp(Bottom + 16, area.Top, area.Bottom), area.Right, area.Bottom)
            };
            var available = candidates.Where(r => r.Width >= 240 && r.Height >= 180).OrderByDescending(r => (long)r.Width * r.Height).FirstOrDefault();
            if (available.IsEmpty) available = area;
            preview.Present(entries, 0, screen, (int)scale.Value, "Size preview — Save to keep", false, available);
            previewTimer.Stop(); previewTimer.Start();
        }
        selector.SelectedIndexChanged += (_, _) => { if (changing) return; StoreDisplay(); selectedDevice = ((DisplayChoice)selector.SelectedItem!).Device; LoadDisplay(); preview.Dismiss(); };
        customize.CheckedChanged += (_, _) =>
        {
            if (changing) return;
            if (customize.Checked) displays[selectedDevice] = defaults;
            else displays.Remove(selectedDevice);
            LoadDisplay();
        };
        slider.ValueChanged += (_, _) => { if (scale.Value != slider.Value) scale.Value = slider.Value; };
        scale.ValueChanged += (_, _) => { if (slider.Value != (int)scale.Value) slider.Value = (int)scale.Value; if (!changing && live.Checked) ShowPreview(); };
        previewButton.Click += (_, _) => ShowPreview();
        live.CheckedChanged += (_, _) => { if (!live.Checked) preview.Dismiss(); };
        previewTimer.Tick += (_, _) => { previewTimer.Stop(); preview.Dismiss(); };
        cancel.Click += (_, _) => Close();
        save.Click += (_, _) =>
        {
            try
            {
                StoreDisplay();
                var next = current with { FocusedMonitor = focus.Checked, IncludeMinimized = defaults.IncludeMinimized, ShowOverlay = overlay.Checked, OverlayScalePercent = defaults.ScalePercent, Displays = new(displays), ExcludedApps = AppRules.Parse(exclusions.Text), CycleKey = (Keys)key.SelectedItem! };
                next.Save(); StartupIntegration.Set(startup.Checked); apply(next); Close();
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException) { MessageBox.Show(this, ex.Message, "Could not save settings"); }
        };
        var general = Ui.Page("General", "Make switching windows feel natural.");
        general.Controls.Add(Ui.Card("Behavior", "The mouse determines the active display by default.", overlay, focus, startup));
        general.Controls.Add(Ui.Card("Keyboard shortcut", "Hold Alt and press your chosen key. Add Shift to go backward.", key, Ui.Label("Arrows navigate · Type to search · Enter selects · Esc cancels", true, 9)));
        var appearance = Ui.Page("Displays & size", "A comfortable switcher on every screen.");
        appearance.Controls.Add(Ui.Card("Display", "Use shared defaults, or personalize a connected display.", selector, customize));
        var sizeRow = new FlowLayoutPanel { AutoSize = true, WrapContents = false }; sizeRow.Controls.AddRange([scale, Ui.Label("%   ·   50% compact — 200% large", true)]);
        appearance.Controls.Add(Ui.Card("Switcher size", "Cards, previews, and text scale together.", slider, sizeRow, live, previewButton, minimized));
        var apps = Ui.Page("App exclusions", "Keep the native shortcut in the apps you choose.");
        apps.Controls.Add(Ui.Card("Excluded applications", "Enter one executable name per line, or choose an application below.", exclusions, browse));
        apps.Controls.Add(Ui.Card("How it works", "A gesture that starts in an excluded app stays native until you release Alt.", Ui.Label("Names are exact matches, such as mstsc.exe.\nThese apps still appear when switching from other applications.", true, 9)));
        var body = new Panel { Dock = DockStyle.Fill, BackColor = Ui.Background };
        body.Controls.AddRange([general, appearance, apps]);
        var sidebar = new FlowLayoutPanel { Dock = DockStyle.Left, Width = 190, Padding = new Padding(18, 26, 14, 16), FlowDirection = FlowDirection.TopDown, WrapContents = false, BackColor = Ui.Surface };
        sidebar.Controls.Add(Ui.Label("BetterAltTab", size: 15, style: FontStyle.Bold));
        sidebar.Controls.Add(Ui.Label("YOUR WORKSPACE", true, 8));
        var navigation = new List<(Button Button, Control Page)>();
        foreach (var item in new[] { ("General", (Control)general), ("Displays & size", (Control)appearance), ("App exclusions", (Control)apps) })
        {
            var button = new Button { Text = item.Item1, UseMnemonic = false, Width = 155, Height = 42, TextAlign = ContentAlignment.MiddleLeft, Margin = new Padding(0, 6, 0, 0), AccessibleName = item.Item1 + " settings" };
            navigation.Add((button, item.Item2)); sidebar.Controls.Add(button);
            button.Click += (_, _) => ShowPage(item.Item2);
        }
        var troubleshooting = new Button { Text = "Troubleshooting", Width = 155, Margin = new Padding(0, 28, 0, 0) };
        troubleshooting.Click += (_, _) => Diagnostics.ShowWindow(); sidebar.Controls.Add(troubleshooting);
        void ShowPage(Control target)
        {
            preview.Dismiss();
            foreach (var item in navigation)
            {
                item.Page.Visible = item.Page == target;
                Ui.StyleButton(item.Button); item.Button.BackColor = item.Page == target ? Ui.Background : Ui.Surface;
                item.Button.AutoSize = false;
                item.Button.ForeColor = item.Page == target ? Ui.Accent : Ui.Text;
                item.Button.FlatAppearance.BorderColor = item.Page == target ? Ui.Accent : Ui.Surface;
            }
            target.BringToFront();
        }
        var footer = new Panel { Dock = DockStyle.Bottom, Height = 72, Padding = new Padding(24, 14, 24, 14), BackColor = Ui.Surface };
        var note = Ui.Label("Changes apply after saving.", true, 9); note.Dock = DockStyle.Left; note.Padding = new Padding(0, 10, 0, 0);
        footer.Controls.Add(note); footer.Controls.Add(buttons);
        Controls.Add(body); Controls.Add(sidebar); Controls.Add(footer);
        Ui.StyleControls(this); Ui.StyleButton(save, true); ShowPage(general);
        troubleshooting.AutoSize = false; troubleshooting.Width = 155; troubleshooting.Height = 44; troubleshooting.Padding = new Padding(4);
        AcceptButton = save; CancelButton = cancel;
        changing = true; selector.SelectedIndex = 0; changing = false; LoadDisplay();
        ResumeLayout(true); PerformAutoScale();
    }
    protected override void Dispose(bool disposing)
    {
        if (disposing) { previewTimer.Dispose(); preview.Dispose(); windows.Dispose(); }
        base.Dispose(disposing);
    }
}
