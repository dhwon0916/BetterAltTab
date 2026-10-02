namespace BetterAltTab;

internal sealed class DiagnosticsForm : Form
{
    public DiagnosticsForm()
    {
        Text = "BetterAltTab · Troubleshooting"; StartPosition = FormStartPosition.CenterScreen;
        Ui.StyleWindow(this); ClientSize = new Size(940, 620); MinimumSize = new Size(760, 480);
        var header = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 102, Padding = new Padding(26, 20, 26, 8), FlowDirection = FlowDirection.TopDown, WrapContents = false };
        header.Controls.Add(Ui.Label("Troubleshooting", size: 22, style: FontStyle.Bold));
        header.Controls.Add(Ui.Label("Record a problem, inspect what happened, and export only when you need to.", true));
        var enabled = new CheckBox { Text = "Record events", Checked = Diagnostics.Enabled, AutoSize = true, Margin = new Padding(0, 10, 28, 0) };
        var refresh = new Button { Text = "Refresh" }; var clear = new Button { Text = "Clear" }; var export = new Button { Text = "Export log…" };
        var toolbar = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 64, Padding = new Padding(26, 8, 26, 8), WrapContents = false };
        toolbar.Controls.AddRange([enabled, refresh, clear, export]);
        var grid = new DataGridView
        {
            Dock = DockStyle.Fill,
            ReadOnly = true,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            AllowUserToResizeRows = false,
            RowHeadersVisible = false,
            BorderStyle = BorderStyle.None,
            BackgroundColor = Ui.Surface,
            GridColor = Ui.Border,
            CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            MultiSelect = false,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
            EnableHeadersVisualStyles = false,
            ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing,
            ColumnHeadersHeight = 38
        };
        grid.DefaultCellStyle = new DataGridViewCellStyle { BackColor = Ui.Surface, ForeColor = Ui.Text, SelectionBackColor = Ui.Dark ? Color.FromArgb(43, 65, 87) : Color.FromArgb(222, 237, 253), SelectionForeColor = Ui.Text, Padding = new Padding(10, 5, 10, 5), Font = new Font("Segoe UI", 9) };
        grid.ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle { BackColor = Ui.Background, ForeColor = Ui.Muted, Padding = new Padding(10, 0, 10, 0), Font = new Font("Segoe UI", 9, FontStyle.Bold) };
        grid.RowTemplate.Height = 34;
        grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "TIME", Width = 170, AutoSizeMode = DataGridViewAutoSizeColumnMode.None, SortMode = DataGridViewColumnSortMode.NotSortable });
        grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "EVENT", SortMode = DataGridViewColumnSortMode.NotSortable });
        var empty = Ui.Label("No events recorded yet.\n\nTurn on Record events, try the switcher, then select Refresh.", true, 12);
        empty.AutoSize = false; empty.Dock = DockStyle.Fill; empty.TextAlign = ContentAlignment.MiddleCenter; empty.BackColor = Ui.Surface;
        var log = new Panel { Dock = DockStyle.Fill, Padding = new Padding(26, 0, 26, 0) }; log.Controls.Add(grid); log.Controls.Add(empty);
        var footer = new Panel { Dock = DockStyle.Bottom, Height = 70, Padding = new Padding(26, 12, 26, 10) };
        var status = Ui.Label("", size: 9, style: FontStyle.Bold); status.Dock = DockStyle.Top;
        var privacy = Ui.Label("Local memory only · Last 1,000 events · No window titles or search text", true, 9); privacy.Dock = DockStyle.Bottom;
        footer.Controls.Add(status); footer.Controls.Add(privacy);
        void Reload()
        {
            var snapshot = Diagnostics.Snapshot(); grid.Rows.Clear();
            foreach (var line in snapshot.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries))
            {
                int separator = line.IndexOf(' ');
                string stamp = separator < 0 ? "" : line[..separator];
                if (DateTimeOffset.TryParse(stamp, out var date)) stamp = date.ToString("HH:mm:ss.fff");
                grid.Rows.Add(stamp, separator < 0 ? line : line[(separator + 1)..]);
            }
            empty.Visible = grid.Rows.Count == 0; export.Enabled = clear.Enabled = grid.Rows.Count > 0;
            status.Text = $"{(Diagnostics.Enabled ? "● Recording" : "○ Recording off")}   ·   {grid.Rows.Count} events";
            status.ForeColor = Diagnostics.Enabled ? Ui.Accent : Ui.Muted;
            if (grid.Rows.Count > 0) grid.FirstDisplayedScrollingRowIndex = grid.Rows.Count - 1;
        }
        enabled.CheckedChanged += (_, _) => { Diagnostics.Enabled = enabled.Checked; Reload(); };
        refresh.Click += (_, _) => Reload(); clear.Click += (_, _) => { Diagnostics.Clear(); Reload(); };
        export.Click += (_, _) =>
        {
            using var dialog = new SaveFileDialog { Filter = "Text log|*.txt", FileName = "BetterAltTab-diagnostics.txt" };
            if (dialog.ShowDialog(this) != DialogResult.OK) return;
            try { File.WriteAllText(dialog.FileName, Diagnostics.Snapshot()); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { MessageBox.Show(this, ex.Message, "Could not export log"); }
        };
        Controls.Add(log); Controls.Add(footer); Controls.Add(toolbar); Controls.Add(header);
        Ui.StyleControls(toolbar); Ui.StyleButton(export, true); Reload();
        ResumeLayout(true); PerformAutoScale();
    }
}
