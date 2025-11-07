using ComputeSharp;

namespace WpfApp;

[ThreadGroupSize(DefaultThreadGroupSizes.XY)]
[GeneratedComputeShaderDescriptor]
public readonly partial struct AverageShader(
    ReadWriteBuffer<uint> layers,
    ReadWriteTexture2D<Rgba64, Float4> aggregate,
    int numLayers) : IComputeShader
{
    public void Execute()
    {
        var layerOffset = ThreadIds.Y * aggregate.Width + ThreadIds.X;
        var layerSize = aggregate.Width * aggregate.Height;
        var sum = new Float4();
        for (var z = 0; z < numLayers; z++)
        {
            var pixelRgb = layers[layerOffset + z * layerSize];
            var pixel = new Float4(
                pixelRgb & 0xff,
                (pixelRgb >> 8) & 0xff,
                (pixelRgb >> 16) & 0xff,
                (pixelRgb >> 24) & 0xff
            ) / 255.0f;
            sum += pixel;
        }
        aggregate[ThreadIds.XY] = sum / numLayers;
    }
}