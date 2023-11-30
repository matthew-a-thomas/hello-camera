namespace WpfApp;

public static class EnumerableExtensions
{
    public static T? FirstOrNull<T>(this IEnumerable<T> items)
        where T : struct
    {
        foreach (var item in items)
        {
            return item;
        }
        return null;
    }
}