namespace WpfApp;

public sealed class SourceTypeDecoded : IStaticAttributeFilter<bool>
{
    static readonly Guid Guid = System.Guid.Parse("ea031a62-8bbb-43c5-b5c4-572d2d231c18");

    public static IEnumerable<bool> Get(IReadOnlyDictionary<Guid, object> attributes)
    {
        if (!attributes.TryGetValue(Guid, out var value) || value is not int valueInt)
            yield return false;
        else
            yield return valueInt != 0;
    }
}