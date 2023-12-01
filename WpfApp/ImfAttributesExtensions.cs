namespace WpfApp;

using Vortice.MediaFoundation;

public static class ImfAttributesExtensions
{
    public static IEnumerable<(Guid Key, object Value)> GetAllAttributes(this IMFAttributes attributes)
    {
        attributes.LockStore();
        try
        {
            for (var i = 0U; i < attributes.Count; i++)
            {
                var value = attributes.GetByIndex(i, out var key);
                yield return (key, value);
            }
        }
        finally
        {
            attributes.UnlockStore();
        }
    }
}