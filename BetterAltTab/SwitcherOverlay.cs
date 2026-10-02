using System.Drawing.Drawing2D;
using Microsoft.Win32;

namespace BetterAltTab;

internal sealed class SwitcherOverlay : Form
{
    private IReadOnlyList<WindowEntry> windows = Array.Empty<WindowEntry>();
    private readonly Dictionary<nint, Icon> icons = new();
    private readonly Dictionary<nint, nint> thumbnails = new();
    private readonly HashSet<nint> visiblePreviews = new();
    private CardLayout layout = CardLayout.Create(0, 0, new Size(1280, 720), 1);
    private int selected;
    private string? display;
    private bool light;
    private string status = "";
    private bool canClose = true;
    private float userScale = 1f;
    private int hovered = -1;
    private bool hoverClose;
    private readonly ToolTip tips = new() { ShowAlways = true, InitialDelay = 500 };
    private Font? bodyFont, captionFont, headingFont;
    private bool disposed;
    private float UiScale => DeviceDpi / 96f * userScale;
    public event Action<int>? WindowClicked;
    public event Action<int>? CloseClicked;
    public int Columns => layout.Columns;
    public int PreviewCount => visiblePreviews.Count;
    internal int PreviewUpdateCount { get; private set; }
    public SwitcherOverlay()
    {
        FormBorderStyle = FormBorderStyle.None; ShowInTaskbar = false; TopMost = true;
        StartPosition = FormStartPosition.Manual; DoubleBuffered = true; AutoScaleMode = AutoScaleMode.None;
        AccessibleName = "Window switcher"; RefreshTheme();
    }
    protected override bool ShowWithoutActivation => true;
    protected override CreateParams CreateParams { get { var p = base.CreateParams; p.ExStyle |= 0x08000080; return p; } }
    protected override void WndProc(ref Message m)
    {
        if (m.Msg == 0x21) { m.Result = 3; return; }
        base.WndProc(ref m);
    }
    private void RefreshTheme()
    {
        using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
        light = key?.GetValue("AppsUseLightTheme") is int value && value != 0;
        BackColor = light ? Color.FromArgb(246, 247, 249) : Color.FromArgb(25, 27, 31);
        ForeColor = light ? Color.FromArgb(28, 28, 28) : Color.FromArgb(245, 245, 245);
    }
    public void Present(IReadOnlyList<WindowEntry> entries, int index, Screen monitor, int scalePercent = 100, string statusText = "", bool allowClose = true, Rectangle? areaOverride = null)
    {
        bool entriesChanged = !ReferenceEquals(windows, entries);
        var previousLayout = layout;
        status = statusText; canClose = allowClose;
        windows = entries; selected = index;
        float nextScale = Math.Clamp(scalePercent, 50, 200) / 100f;
        if (bodyFont == null || nextScale != userScale)
        {
            bodyFont?.Dispose(); captionFont?.Dispose(); headingFont?.Dispose();
            bodyFont = new Font("Segoe UI", 12 * nextScale);
            captionFont = new Font("Segoe UI", 10 * nextScale);
            headingFont = new Font("Segoe UI", 13 * nextScale, FontStyle.Bold);
        }
        userScale = nextScale;
        if (!Visible) { RefreshTheme(); _ = Handle; Location = monitor.WorkingArea.Location; }
        else if (display != monitor.DeviceName) Location = monitor.WorkingArea.Location;
        display = monitor.DeviceName;
        var workArea = areaOverride ?? monitor.WorkingArea;
        int footer = (int)(38 * UiScale), header = (int)(52 * UiScale);
        layout = CardLayout.Create(entries.Count, index, new Size(workArea.Width, Math.Max(100, workArea.Height - footer - header)), UiScale);
        layout = layout with { Size = new Size(layout.Size.Width, layout.Size.Height + footer + header), Cards = layout.Cards.Select(r => new Rectangle(r.X, r.Y + header, r.Width, r.Height)).ToArray() };
        Size = layout.Size;
        Location = new Point(workArea.Left + (workArea.Width - Width) / 2, workArea.Top + (workArea.Height - Height) / 2);
        int corner = 2, dark = light ? 0 : 1;
        Native.DwmSetWindowAttribute(Handle, 33, ref corner, 4);
        Native.DwmSetWindowAttribute(Handle, 20, ref dark, 4);
        if (entriesChanged || !Visible || previousLayout.First != layout.First || previousLayout.Size != layout.Size || !previousLayout.Cards.SequenceEqual(layout.Cards)) UpdatePreviews();
        AccessibleDescription = entries.Count > 0 ? entries[index].Title : "No matching windows";
        if (!Visible) Show();
        Invalidate();
    }
    private Rectangle PreviewBounds(Rectangle card)
    {
        int inset = (int)(12 * UiScale), header = (int)(50 * UiScale);
        return new Rectangle(card.X + inset, card.Y + header, Math.Max(1, card.Width - inset * 2), Math.Max(1, card.Height - header - inset));
    }
    private void UpdatePreviews()
    {
        var shown = Enumerable.Range(layout.First, layout.Cards.Count).Select(i => windows[i].Handle).ToHashSet();
        foreach (var hwnd in thumbnails.Keys.Where(h => !shown.Contains(h)).ToArray())
        { Native.DwmUnregisterThumbnail(thumbnails[hwnd]); thumbnails.Remove(hwnd); }
        visiblePreviews.Clear();
        for (int slot = 0; slot < layout.Cards.Count; slot++)
        {
            var hwnd = windows[layout.First + slot].Handle;
            if (!thumbnails.TryGetValue(hwnd, out var thumbnail))
            {
                if (Native.DwmRegisterThumbnail(Handle, hwnd, out thumbnail) != 0) continue;
                thumbnails[hwnd] = thumbnail;
            }
            if (Native.DwmQueryThumbnailSourceSize(thumbnail, out var source) != 0 || source.Width < 1 || source.Height < 1) continue;
            var dest = CardLayout.Fit(source, PreviewBounds(layout.Cards[slot]));
            var properties = new Native.ThumbnailProperties
            {
                Flags = 1 | 4 | 8 | 16,
                Destination = new Native.Rect { Left = dest.Left, Top = dest.Top, Right = dest.Right, Bottom = dest.Bottom },
                Opacity = 255,
                Visible = true,
                ClientOnly = false
            };
            PreviewUpdateCount++;
            if (Native.DwmUpdateThumbnailProperties(thumbnail, ref properties) == 0) visiblePreviews.Add(hwnd);
        }
    }
    public void RefreshSource(nint hwnd) { if (Visible && thumbnails.ContainsKey(hwnd)) UpdatePreviews(); }
    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e); e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        float s = UiScale; var font = bodyFont ?? Font;
        var muted = light ? Color.FromArgb(99, 109, 125) : Color.FromArgb(159, 168, 183);
        var heading = status.StartsWith("Search: ", StringComparison.Ordinal) ? status : canClose ? "Windows on this display" : "Size preview";
        TextRenderer.DrawText(e.Graphics, heading, headingFont, new Rectangle((int)(24 * s), (int)(18 * s), Math.Max(1, Width - (int)(150 * s)), (int)(32 * s)), ForeColor, TextFormatFlags.EndEllipsis | TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix);
        TextRenderer.DrawText(e.Graphics, windows.Count == 0 ? "0 windows" : $"{selected + 1} / {windows.Count}", captionFont, new Rectangle(Width - (int)(120 * s), (int)(21 * s), (int)(92 * s), (int)(28 * s)), muted, TextFormatFlags.Right | TextFormatFlags.SingleLine);
        for (int slot = 0; slot < layout.Cards.Count; slot++)
        {
            var index = layout.First + slot; if (index >= windows.Count) break;
            var card = layout.Cards[slot]; using var path = Rounded(card, 8 * s);
            using var fill = new SolidBrush(light ? (index == hovered ? Color.FromArgb(235, 242, 250) : Color.White) : (index == hovered ? Color.FromArgb(44, 49, 58) : Color.FromArgb(35, 38, 43)));
            e.Graphics.FillPath(fill, path);
            using var pen = new Pen(index == selected ? (light ? Color.FromArgb(0, 103, 192) : Color.FromArgb(115, 187, 255)) : (light ? Color.FromArgb(221, 225, 232) : Color.FromArgb(56, 62, 71)), index == selected ? 2 * s : s);
            e.Graphics.DrawPath(pen, path);
            var icon = GetIcon(windows[index].Handle);
            e.Graphics.DrawIcon(icon, new Rectangle(card.X + (int)(14 * s), card.Y + (int)(13 * s), (int)(24 * s), (int)(24 * s)));
            var title = new Rectangle(card.X + (int)(48 * s), card.Y + (int)(8 * s), Math.Max(1, card.Width - (int)(100 * s)), (int)(34 * s));
            TextRenderer.DrawText(e.Graphics, windows[index].Title, font, title, ForeColor, TextFormatFlags.EndEllipsis | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix);
            if (canClose)
            {
                var close = CloseBounds(card);
                if (index == hovered && hoverClose)
                { using var closeFill = new SolidBrush(Color.FromArgb(196, 43, 52)); using var closePath = Rounded(close, 5 * s); e.Graphics.FillPath(closeFill, closePath); }
                TextRenderer.DrawText(e.Graphics, "×", font, close, index == hovered && hoverClose ? Color.White : muted, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            }
            if (!visiblePreviews.Contains(windows[index].Handle))
            {
                var preview = PreviewBounds(card); var size = (int)(48 * s);
                e.Graphics.DrawIcon(icon, new Rectangle(preview.X + (preview.Width - size) / 2, preview.Y + (preview.Height - size) / 2, size, size));
            }
        }
        if (windows.Count == 0)
            TextRenderer.DrawText(e.Graphics, "No matching windows", font, new Rectangle(0, 0, Width, Height - (int)(40 * s)), ForeColor, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        var footerText = !canClose ? "Preview only · Save in Settings to keep changes" : status.StartsWith("Search: ", StringComparison.Ordinal) ? "Backspace to edit   ·   Enter to select   ·   Esc to cancel" : "Type to search   ·   Arrows to move   ·   Release Alt to select";
        TextRenderer.DrawText(e.Graphics, footerText, captionFont, new Rectangle((int)(24 * s), Height - (int)(38 * s), Width - (int)(48 * s), (int)(26 * s)), muted, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix);
    }
    private static GraphicsPath Rounded(Rectangle rect, float radius)
    {
        var path = new GraphicsPath(); var d = radius * 2;
        path.AddArc(rect.Left, rect.Top, d, d, 180, 90); path.AddArc(rect.Right - d, rect.Top, d, d, 270, 90);
        path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90); path.AddArc(rect.Left, rect.Bottom - d, d, d, 90, 90);
        path.CloseFigure(); return path;
    }
    protected override void OnMouseClick(MouseEventArgs e)
    {
        base.OnMouseClick(e); if (e.Button != MouseButtons.Left) return;
        for (int slot = 0; slot < layout.Cards.Count; slot++)
            if (layout.Cards[slot].Contains(e.Location))
            {
                if (canClose && CloseBounds(layout.Cards[slot]).Contains(e.Location)) CloseClicked?.Invoke(layout.First + slot);
                else WindowClicked?.Invoke(layout.First + slot);
                break;
            }
    }
    private Rectangle CloseBounds(Rectangle card) => new(card.Right - (int)(42 * UiScale), card.Top + (int)(8 * UiScale), (int)(32 * UiScale), (int)(34 * UiScale));
    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        int next = -1; bool close = false;
        for (int slot = 0; slot < layout.Cards.Count; slot++)
            if (layout.Cards[slot].Contains(e.Location)) { next = layout.First + slot; close = canClose && CloseBounds(layout.Cards[slot]).Contains(e.Location); break; }
        if (next == hovered && close == hoverClose) return;
        hovered = next; hoverClose = close; Cursor = next >= 0 && canClose ? Cursors.Hand : Cursors.Default;
        tips.SetToolTip(this, next >= 0 && next < windows.Count ? (close ? "Close " : "") + windows[next].Title : ""); Invalidate();
    }
    protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); hovered = -1; hoverClose = false; tips.SetToolTip(this, ""); Invalidate(); }
    private Icon GetIcon(nint hwnd)
    {
        if (icons.TryGetValue(hwnd, out var cached)) return cached;
        Native.SendMessageTimeout(hwnd, 0x7F, 1, 0, 2, 15, out var handle);
        if (handle == 0) handle = Native.GetClassLongPtr(hwnd, -14);
        Icon icon;
        try { icon = handle == 0 ? (Icon)SystemIcons.Application.Clone() : (Icon)Icon.FromHandle(handle).Clone(); }
        catch (ArgumentException) { icon = (Icon)SystemIcons.Application.Clone(); }
        return icons[hwnd] = icon;
    }
    public void Dismiss()
    {
        Hide(); hovered = -1; hoverClose = false; tips.SetToolTip(this, ""); foreach (var thumbnail in thumbnails.Values) Native.DwmUnregisterThumbnail(thumbnail);
        thumbnails.Clear(); visiblePreviews.Clear(); windows = Array.Empty<WindowEntry>();
        foreach (var icon in icons.Values) icon.Dispose(); icons.Clear();
    }
    protected override void Dispose(bool disposing)
    {
        if (disposing && !disposed)
        {
            Dismiss(); tips.Dispose();
            bodyFont?.Dispose(); captionFont?.Dispose(); headingFont?.Dispose();
            disposed = true;
        }
        base.Dispose(disposing);
    }
}
