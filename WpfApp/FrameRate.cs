namespace WpfApp;

using Vortice.MediaFoundation;

public sealed class FrameRate : IStaticAttributeFilter<double>
{
    public static IEnumerable<double> Get(IReadOnlyDictionary<Guid, object> attributes)
    {
        if (!attributes.TryGetValue(MediaTypeAttributeKeys.FrameRate, out var framerateRaw) || framerateRaw is not long framerateLong)
        {
            yield return 0;
        }
        else
        {
            var (numerator, denominator) = ImfHelpers.Tear((ulong)framerateLong);
            yield return (double)numerator / denominator;
        }
    }
}