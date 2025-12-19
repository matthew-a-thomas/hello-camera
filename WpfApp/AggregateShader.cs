using ComputeSharp;

namespace WpfApp;

[ThreadGroupSize(DefaultThreadGroupSizes.XY)]
[GeneratedComputeShaderDescriptor]
public readonly partial struct AggregateShader(
    ReadWriteTexture3D<Rgba32, Float4> layers,
    ReadWriteTexture2D<Rgba64, Float4> aggregate,
    ReadWriteTexture2D<Rgba32, Float4> output,
    int numLayers) : IComputeShader
{
    public void Execute()
    {
        var oldMedianPixel = aggregate[ThreadIds.XY];
        var numerator = new Float4();
        var denominator = 0.0f;
        for (var z = 0; z < numLayers; z++)
        {
            var id = new Int3(ThreadIds.XY, z);
            var pixel = layers[id];
            var distance = Hlsl.Max(0.001f, Hlsl.Distance(pixel, oldMedianPixel));
            numerator += pixel / distance;
            denominator += 1.0f / distance;
        }
        var newMedianPixel = numerator / denominator;
        aggregate[ThreadIds.XY] = newMedianPixel;
        output[ThreadIds.XY] = Hlsl.Pow(newMedianPixel, 2f);
    }
}