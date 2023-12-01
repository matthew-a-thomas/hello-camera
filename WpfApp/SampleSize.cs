namespace WpfApp;

using Vortice.MediaFoundation;

public sealed class SampleSize : IStaticAttributeFilter<uint>
{
    public static IEnumerable<uint> Get(IReadOnlyDictionary<Guid, object> attributes)
    {
        if (!attributes.TryGetValue(MediaTypeAttributeKeys.SampleSize, out var sampleSizeRaw) || sampleSizeRaw is not int sampleSizeInt)
            yield return default;
        else
            yield return (uint)sampleSizeInt;
    }
}