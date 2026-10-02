using Microsoft.Win32;

namespace BetterAltTab;

internal static class Ui
{
    public static bool Dark { get; private set; } = ReadDarkPreference();
    public static void RefreshTheme() => Dark = ReadDarkPreference();
    private static bool ReadDarkPreference()
    {
        using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
        return key?.GetValue("AppsUseLightTheme") is not int value || value == 0;
    }
    public static Color Background => Dark ? Color.FromArgb(25, 27, 31) : Color.FromArgb(246, 247, 249);
    public static Color Surface => Dark ? Color.FromArgb(35, 38, 43) : Color.White;
    public static Color Text => Dark ? Color.FromArgb(241, 243, 247) : Color.FromArgb(27, 32, 41);
    public static Color Muted => Dark ? Color.FromArgb(159, 168, 183) : Color.FromArgb(99, 109, 125);
    public static Color Border => Dark ? Color.FromArgb(56, 62, 71) : Color.FromArgb(221, 225, 232);
    public static Color Accent => Dark ? Color.FromArgb(115, 187, 255) : Color.FromArgb(0, 103, 192);
    public static void StyleWindow(Form form)
    {
        RefreshTheme();
        form.SuspendLayout();
        form.Icon = AppIcon.Value;
        form.Font = new Font("Segoe UI", 10);
        form.BackColor = Background; form.ForeColor = Text;
        form.AutoScaleMode = AutoScaleMode.Dpi;
        form.AutoScaleDimensions = new SizeF(96, 96);
        form.HandleCreated += (_, _) => { int dark = Dark ? 1 : 0, corners = 2; Native.DwmSetWindowAttribute(form.Handle, 20, ref dark, 4); Native.DwmSetWindowAttribute(form.Handle, 33, ref corners, 4); };
        form.Shown += (_, _) =>
        {
            var area = Screen.FromControl(form).WorkingArea;
            int width = Math.Max(320, area.Width - 32), height = Math.Max(240, area.Height - 32);
            form.MinimumSize = new Size(Math.Min(form.MinimumSize.Width, width), Math.Min(form.MinimumSize.Height, height));
            if (form.Width > width || form.Height > height)
            {
                form.Size = new Size(Math.Min(form.Width, width), Math.Min(form.Height, height));
                form.Location = new Point(area.Left + (area.Width - form.Width) / 2, area.Top + (area.Height - form.Height) / 2);
            }
        };
    }
    public static void StyleControls(Control parent)
    {
        foreach (Control c in parent.Controls)
        {
            if (c is not System.Windows.Forms.Label) c.ForeColor = Text;
            if (c is TextBoxBase or ComboBox or NumericUpDown) c.BackColor = Surface;
            if (c is CheckBox check) { check.AutoSize = true; check.Margin = new Padding(0, 5, 0, 8); }
            if (c is Button button) StyleButton(button);
            StyleControls(c);
        }
    }
    public static void StyleButton(Button button, bool primary = false)
    {
        button.FlatStyle = FlatStyle.Flat; button.FlatAppearance.BorderSize = primary ? 0 : 1;
        button.FlatAppearance.BorderColor = Border; button.BackColor = primary ? Accent : Surface;
        button.ForeColor = primary ? (Dark ? Color.FromArgb(16, 27, 42) : Color.White) : Text;
        button.Padding = new Padding(12, 5, 12, 5); button.MinimumSize = new Size(96, 36);
        button.AutoSize = true; button.Cursor = Cursors.Hand;
    }
    public static Label Label(string text, bool muted = false, float size = 10, FontStyle style = FontStyle.Regular)
        => new() { Text = text, AutoSize = true, Font = new Font("Segoe UI", size, style), ForeColor = muted ? Muted : Text, Margin = new Padding(0, 0, 0, 8), UseMnemonic = false };
    public static FlowLayoutPanel Page(string title, string subtitle)
    {
        var page = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoScroll = true, Padding = new Padding(26, 24, 26, 16), BackColor = Background };
        page.Controls.Add(Label(title, size: 22, style: FontStyle.Bold));
        page.Controls.Add(Label(subtitle, true));
        page.SizeChanged += (_, _) => FitChildren(page);
        page.ControlAdded += (_, _) => FitChildren(page);
        return page;
    }
    private static void FitChildren(FlowLayoutPanel page)
    {
        int width = Math.Max(200, page.ClientSize.Width - page.Padding.Horizontal - SystemInformation.VerticalScrollBarWidth - 4);
        foreach (Control child in page.Controls)
        {
            if (child is Label label) label.MaximumSize = new Size(width, 0);
            else { child.MinimumSize = new Size(width, 0); child.MaximumSize = new Size(width, 0); child.Width = width; }
        }
    }
    public static FlowLayoutPanel Card(string title, string description, params Control[] controls)
    {
        var card = new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, Padding = new Padding(18, 16, 18, 12), Margin = new Padding(0, 12, 0, 0), BackColor = Surface, MinimumSize = new Size(300, 0) };
        card.Controls.Add(Label(title, size: 11, style: FontStyle.Bold));
        if (description.Length > 0) card.Controls.Add(Label(description, true, 9));
        card.Controls.AddRange(controls);
        card.SizeChanged += (_, _) =>
        {
            int width = Math.Max(100, card.Width - card.Padding.Horizontal);
            foreach (Control child in card.Controls)
            {
                if (child is Label label) label.MaximumSize = new Size(width, 0);
                else if (child is TextBox or ComboBox or TrackBar) child.Width = width;
            }
        };
        return card;
    }
}
