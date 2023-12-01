using ComputeSharp;

namespace WpfApp;

[ThreadGroupSize(DefaultThreadGroupSizes.XY)]
[GeneratedComputeShaderDescriptor]
public readonly partial struct MedianShader(
    ReadWriteTexture3D<Rgba32, Float4> layers,
    ReadWriteTexture2D<Rgba64, Float4> median,
    int numLayers) : IComputeShader
{
    public void Execute()
    {
        var oldMedianPixel = median[ThreadIds.XY];
        var numerator = new Float4();
        var denominator = 0.0f;
        for (var z = 0; z < numLayers; z++)
        {
            var pixel = layers[ThreadIds.X, ThreadIds.Y, z];
            var distance = Hlsl.Max(0.001f, Hlsl.Distance(pixel, oldMedianPixel));
            numerator += pixel / distance;
            denominator += 1.0f / distance;
        }
        var newMedianPixel = numerator / denominator;
        median[ThreadIds.XY] = newMedianPixel;
    }
}