namespace BetterAltTab;

// Single-threaded, message-loop batch: one queued drain per burst of unique keys.
internal sealed class DeferredSet<T> where T : notnull
{
    private readonly HashSet<T> items = new();
    private bool scheduled;
    public bool Add(T item)
    {
        items.Add(item);
        if (scheduled) return false;
        scheduled = true;
        return true;
    }
    public void Remove(T item) => items.Remove(item);
    public T[] Drain()
    {
        var result = items.ToArray();
        Clear();
        return result;
    }
    public void Clear() { scheduled = false; items.Clear(); }
}
