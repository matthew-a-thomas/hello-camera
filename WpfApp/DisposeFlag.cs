namespace WpfApp;

public sealed class DisposeFlag : IDisposable
{
    public bool Disposed { get; private set; }

    public void Dispose() => Disposed = true;
}