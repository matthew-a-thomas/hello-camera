namespace WpfApp;

public sealed record Frame(
    ReadOnlyMemory<byte> Buffer,
    int Stride,
    TimeSpan Timestamp);