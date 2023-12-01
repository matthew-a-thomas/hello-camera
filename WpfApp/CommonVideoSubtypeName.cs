namespace WpfApp;

using Vortice.MediaFoundation;

public sealed class CommonVideoSubtypeName : IStaticAttributeFilter<string>
{
    public static IEnumerable<string> Get(IReadOnlyDictionary<Guid, object> attributes)
    {
        if (!attributes.TryGetValue(MediaTypeAttributeKeys.Subtype, out var rawSubtype) || rawSubtype is not Guid subtype)
            yield break;
        if (ImfHelpers.VideoSubtypeNames.TryGetValue(subtype, out var name))
            yield return name;
    }
}