namespace WpfApp;

public sealed class FrameAvailableEvent(
    ReadOnlyMemory<byte> memory,
    int bitsPerPixel,
    int stride,
    TimeSpan timestamp,
    Action dispose) : IDisposable
{
    Action? _dispose = dispose;
    int _refCount = 1;

    public ReadOnlyMemory<byte> Memory { get; private set; } = memory;
    public int BitsPerPixel { get; } = bitsPerPixel;
    public int Stride { get; } = stride;
    public TimeSpan Timestamp { get; } = timestamp;

    public void Dispose()
    {
        if (Interlocked.Decrement(ref _refCount) != 0)
            return;
        Memory = default;
        Interlocked.Exchange(ref _dispose, null)?.Invoke();
    }

    public void Increment()
    {
        Interlocked.Increment(ref _refCount);
    }
}