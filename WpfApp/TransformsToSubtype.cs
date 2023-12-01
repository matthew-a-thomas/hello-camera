namespace WpfApp;

using Vortice.MediaFoundation;

public sealed class TransformsToSubtype(Guid subtype) : IAttributeFilter<Func<IMFTransform>>
{
    public IEnumerable<Func<IMFTransform>> Get(IReadOnlyDictionary<Guid, object> attributes)
    {
        if (!attributes.TryGetValue(MediaTypeAttributeKeys.Subtype, out var fromSubtypeRaw) || fromSubtypeRaw is not Guid fromSubtype)
            yield break;
        using var collection = ImfHelpers.GetTransforms(fromSubtype, subtype);
        for (var i = 0; i < collection.Count; i++)
        {
            var capturedIndex = i;
            yield return () => collection.Create(capturedIndex);
        }
    }
}