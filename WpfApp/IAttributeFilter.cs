namespace WpfApp;

public interface IAttributeFilter<out TValue>
{
    IEnumerable<TValue> Get(IReadOnlyDictionary<Guid, object> attributes);
}