using System.Runtime.InteropServices;
using ComputeSharp;

namespace WpfApp;

[ThreadGroupSize(DefaultThreadGroupSizes.XY)]
[GeneratedComputeShaderDescriptor]
public readonly partial struct MedianShaderPass(
    ReadWriteTexture3D<Rgba32, Float4> layers,
    ReadWriteTexture2D<Rgba32, Float4> median,
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

public sealed class MedianShader(
    int maxNumLayers,
    int pixelWidth,
    int pixelHeight) : IDisposable
{
    GraphicsDevice? _graphicsDevice;
    ReadWriteTexture3D<Rgba32, Float4>? _layers;
    ReadWriteTexture2D<Rgba32, Float4>? _median;
    int _numLayers;

    public void Dispose()
    {
        _numLayers = 0;
        Interlocked.Exchange(ref _graphicsDevice, null)?.Dispose();
    }

    public void Incorporate(
        ReadOnlySpan<byte> layerIn,
        Span<byte> medianOut)
    {
        var numBytes = pixelWidth * pixelHeight * 4;
        if (layerIn.Length != numBytes || medianOut.Length != numBytes)
            throw new InvalidOperationException("The layer and/or the median have the wrong number of bytes");
        var graphicsDevice = _graphicsDevice ??= GraphicsDevice.GetDefault();
        var layers = _layers ??= graphicsDevice.AllocateReadWriteTexture3D<Rgba32, Float4>(pixelWidth, pixelHeight, maxNumLayers);
        var median = _median ??= graphicsDevice.AllocateReadWriteTexture2D<Rgba32, Float4>(pixelWidth, pixelHeight, AllocationMode.Clear);
        var layerIndex = _numLayers++ % maxNumLayers;
        layers.CopyFrom(
            MemoryMarshal.Cast<byte, Rgba32>(layerIn),
            0,
            0,
            layerIndex,
            pixelWidth,
            pixelHeight,
            1);
        graphicsDevice.For(
            pixelWidth,
            pixelHeight,
            new MedianShaderPass(layers, median, Math.Min(maxNumLayers, _numLayers)));
        median.CopyTo(MemoryMarshal.Cast<byte, Rgba32>(medianOut));
    }
}