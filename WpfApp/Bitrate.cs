namespace WpfApp;

using Vortice.MediaFoundation;

public sealed class Bitrate : IStaticAttributeFilter<uint>
{
    public static IEnumerable<uint> Get(IReadOnlyDictionary<Guid, object> attributes)
    {
        if (!attributes.TryGetValue(MediaTypeAttributeKeys.AvgBitrate, out var bitrateRaw) || bitrateRaw is not int bitrateInt)
            yield return default;
        else
            yield return (uint)bitrateInt;
    }
}