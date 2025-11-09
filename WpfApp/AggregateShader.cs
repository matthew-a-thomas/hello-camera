using ComputeSharp;

namespace WpfApp;

[ThreadGroupSize(DefaultThreadGroupSizes.XY)]
[GeneratedComputeShaderDescriptor]
public readonly partial struct AggregateShader(
    ReadWriteBuffer<uint> layers,
    ReadWriteTexture2D<Rgba64, Float4> aggregate,
    ReadWriteTexture2D<Rgba32, Float4> output,
    int numLayers) : IComputeShader
{
    public void Execute()
    {
        var oldMedianPixel = aggregate[ThreadIds.XY];
        var layerOffset = ThreadIds.Y * aggregate.Width + ThreadIds.X;
        var layerSize = aggregate.Width * aggregate.Height;
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
        aggregate[ThreadIds.XY] = newMedianPixel;
        output[ThreadIds.XY] = Hlsl.Pow(newMedianPixel, 0.5f);
    }
}