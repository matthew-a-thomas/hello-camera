namespace WpfApp;

using ComputeSharp;

[ThreadGroupSize(DefaultThreadGroupSizes.XY)]
[GeneratedComputeShaderDescriptor]
public readonly partial struct BrightenShader(
    ReadWriteTexture2D<Rgba64, Float4> textureIn,
    ReadWriteTexture2D<Rgba32, Float4> textureOut,
    float multiplicationFactor) : IComputeShader
{
    public void Execute()
    {
        textureOut[ThreadIds.XY] = textureIn[ThreadIds.XY] * multiplicationFactor;
    }
}