using ComputeSharp;

namespace WpfApp;

[ThreadGroupSize(DefaultThreadGroupSizes.XY)]
[GeneratedComputeShaderDescriptor]
public readonly partial struct MedianShader(
    ReadWriteBuffer<uint> layers,
    ReadWriteTexture2D<Rgba64, Float4> median,
    int numLayers) : IComputeShader
{
    public void Execute()
    {
        var oldMedianPixel = median[ThreadIds.XY];
        var layerOffset = ThreadIds.Y * median.Width + ThreadIds.X;
        var layerSize = median.Width * median.Height;
        var numerator = new Float4();
        var denominator = 0.0f;
        for (var z = 0; z < numLayers; z++)
        {
            var pixelRgb = layers[layerOffset + z * layerSize];
            var pixel = new Float4(
                pixelRgb & 0xff,
                (pixelRgb >> 8) & 0xff,
                (pixelRgb >> 16) & 0xff,
                (pixelRgb >> 24) & 0xff
            ) / 255.0f;
            var distance = Hlsl.Max(0.001f, Hlsl.Distance(pixel, oldMedianPixel));
            numerator += pixel / distance;
            denominator += 1.0f / distance;
        }
        var newMedianPixel = numerator / denominator;
        median[ThreadIds.XY] = newMedianPixel;
    }
}