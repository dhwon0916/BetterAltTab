using System.Drawing;

namespace BetterAltTab;

internal sealed record CardLayout(Size Size, IReadOnlyList<Rectangle> Cards, int First, int Capacity, int Columns)
{
    public static CardLayout Create(int count, int selected, Size workArea, float scale)
    {
        int padding = Math.Max(4, (int)(24 * scale)), gap = Math.Max(2, (int)(14 * scale));
        int maxWidth = Math.Max(100, (int)(workArea.Width * .88)), maxHeight = Math.Max(100, (int)(workArea.Height * .8));
        int cardWidth = Math.Min((int)(420 * scale), Math.Max(60, maxWidth - 2 * padding));
        int cardHeight = Math.Min((int)(292 * scale), Math.Max(60, maxHeight - 2 * padding - (int)(30 * scale)));
        int columns = Math.Max(1, Math.Min(Math.Min(5, Math.Max(1, count)), (maxWidth - 2 * padding + gap) / (cardWidth + gap)));
        int maxRows = Math.Max(1, Math.Min(3, (maxHeight - 2 * padding + gap - (int)(30 * scale)) / (cardHeight + gap)));
        int capacity = columns * maxRows, first = Math.Max(0, selected) / capacity * capacity;
        int shown = Math.Min(capacity, Math.Max(0, count - first));
        int rows = Math.Max(1, (shown + columns - 1) / columns);
        int width = columns * cardWidth + (columns - 1) * gap + padding * 2;
        int height = rows * cardHeight + (rows - 1) * gap + padding * 2;
        var cards = new List<Rectangle>();
        for (int i = 0; i < shown; i++)
        {
            int row = i / columns, rowCount = Math.Min(columns, shown - row * columns);
            int left = (width - rowCount * cardWidth - (rowCount - 1) * gap) / 2;
            cards.Add(new Rectangle(left + i % columns * (cardWidth + gap), padding + row * (cardHeight + gap), cardWidth, cardHeight));
        }
        return new(new Size(width, height), cards, first, capacity, columns);
    }
    public static Rectangle Fit(Size source, Rectangle bounds)
    {
        double ratio = Math.Min((double)bounds.Width / source.Width, (double)bounds.Height / source.Height);
        int width = Math.Max(1, (int)(source.Width * ratio)), height = Math.Max(1, (int)(source.Height * ratio));
        return new(bounds.X + (bounds.Width - width) / 2, bounds.Y + (bounds.Height - height) / 2, width, height);
    }
}
