namespace BetterAltTab;

// Owned by the UI thread. Replacing an async request cancels it; only the
// request itself disposes its source after its awaited work has unwound.
internal sealed class LatestOperation : IDisposable
{
    private Lease? current;
    private bool disposed;

    public Lease Begin()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        current?.Cancel();
        return current = new Lease(this);
    }

    public void Cancel() => current?.Cancel();

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        Cancel();
    }

    internal sealed class Lease : IDisposable
    {
        private readonly LatestOperation owner;
        private readonly CancellationTokenSource source = new();
        private bool disposed;
        internal Lease(LatestOperation owner) { this.owner = owner; Token = source.Token; }
        public CancellationToken Token { get; }
        public bool IsCancellationRequested => Token.IsCancellationRequested;
        internal void Cancel() { if (!disposed) source.Cancel(); }
        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            if (ReferenceEquals(owner.current, this)) owner.current = null;
            source.Dispose();
        }
    }
}
