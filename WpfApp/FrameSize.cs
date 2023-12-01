namespace WpfApp;

using Vortice.MediaFoundation;

public sealed class FrameSize : IStaticAttributeFilter<(uint Width, uint Height)>
{
    public static IEnumerable<(uint Width, uint Height)> Get(IReadOnlyDictionary<Guid, object> attributes)
    {
        if (!attributes.TryGetValue(MediaTypeAttributeKeys.FrameSize, out var frameSizeRaw) || frameSizeRaw is not long frameSizeLong)
            yield return default;
        else
            yield return ImfHelpers.Tear((ulong)frameSizeLong);
    }
}