namespace WpfApp;

public interface IStaticAttributeFilter<out TValue>
{
    public static abstract IEnumerable<TValue> Get(IReadOnlyDictionary<Guid, object> attributes);
}