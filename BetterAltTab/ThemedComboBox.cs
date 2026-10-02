using System.Drawing.Drawing2D;

namespace BetterAltTab;

// Keep native keyboard/accessibility/dropdown behavior, but paint the closed
// field consistently with the rest of the window, including WM_PRINT captures.
internal sealed class ThemedComboBox : ComboBox
{
    public ThemedComboBox()
    {
        DropDownStyle = ComboBoxStyle.DropDownList; DrawMode = DrawMode.OwnerDrawFixed;
        FlatStyle = FlatStyle.Flat; ItemHeight = 28;
    }
    protected override void OnDrawItem(DrawItemEventArgs e)
    {
        bool selected = (e.State & DrawItemState.Selected) != 0;
        using var fill = new SolidBrush(selected ? (Ui.Dark ? Color.FromArgb(43, 65, 87) : Color.FromArgb(222, 237, 253)) : Ui.Surface);
        e.Graphics.FillRectangle(fill, e.Bounds);
        var text = e.Index >= 0 ? GetItemText(Items[e.Index]) ?? "" : Text;
        TextRenderer.DrawText(e.Graphics, text, Font, new Rectangle(e.Bounds.X + 10, e.Bounds.Y, Math.Max(1, e.Bounds.Width - 20), e.Bounds.Height), Ui.Text, TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
    }
    protected override void OnSelectedIndexChanged(EventArgs e) { base.OnSelectedIndexChanged(e); Invalidate(); }
    protected override void WndProc(ref Message m)
    {
        base.WndProc(ref m);
        if (m.Msg == 0xF) { using var g = Graphics.FromHwnd(Handle); DrawFace(g); }
        else if ((m.Msg == 0x317 || m.Msg == 0x318) && m.WParam != 0) { using var g = Graphics.FromHdc(m.WParam); DrawFace(g); }
    }
    private void DrawFace(Graphics graphics)
    {
        var rect = ClientRectangle; if (rect.Width < 2 || rect.Height < 2) return;
        using var fill = new SolidBrush(Ui.Surface); graphics.FillRectangle(fill, rect);
        using var pen = new Pen(Focused ? Ui.Accent : Ui.Border); graphics.DrawRectangle(pen, 0, 0, rect.Width - 1, rect.Height - 1);
        float scale = DeviceDpi / 96f;
        var text = SelectedItem == null ? Text : GetItemText(SelectedItem) ?? "";
        TextRenderer.DrawText(graphics, text, Font, new Rectangle((int)(10 * scale), 0, Math.Max(1, Width - (int)(44 * scale)), Height), Enabled ? Ui.Text : Ui.Muted, TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using var arrow = new Pen(Ui.Muted, Math.Max(1, scale)); float x = Width - 18 * scale, y = Height / 2f;
        graphics.DrawLines(arrow, [new PointF(x - 4 * scale, y - 2 * scale), new PointF(x, y + 2 * scale), new PointF(x + 4 * scale, y - 2 * scale)]);
    }
}
