namespace WpfApp;

public sealed class BlobCache<T> : IDisposable
{
    readonly object _gate = new();
    int? _lastSize;
    Stack<T[]>? _arrays = new();

    public void Dispose()
    {
        Stack<T[]> arrays;
        lock (_gate)
        {
            if (_arrays is null)
                return;
            (arrays, _arrays) = (_arrays, null);
        }
        while (arrays.TryPop(out var array))
        {
            Array.Clear(array);
        }
    }

    public T[] Get(int size)
    {
        Stack<T[]>? oldArrays = null;
        try
        {
            lock (_gate)
            {
                if (_arrays is null)
                    throw new InvalidOperationException("This " + nameof(BlobCache<T>) + " has been disposed of");
                if (size != _lastSize)
                {
                    (oldArrays, _arrays) = (_arrays, new Stack<T[]>());
                    _lastSize = size;
                }
                else if (_arrays.TryPop(out var array))
                {
                    return array;
                }
            }
        }
        finally
        {
            if (oldArrays is not null)
            {
                while (oldArrays.TryPop(out var oldArray))
                {
                    Array.Clear(oldArray);
                }
            }
        }
        return new T[size];
    }

    public void Return(T[] array)
    {
        lock (_gate)
        {
            if (_arrays is not null && array.Length == _lastSize)
            {
                _arrays.Push(array);
                return;
            }
        }
        Array.Clear(array);
    }
}