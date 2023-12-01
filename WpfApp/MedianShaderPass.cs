using System.Buffers;
using System.Runtime.InteropServices;
using ComputeSharp;

namespace WpfApp;

[ThreadGroupSize(DefaultThreadGroupSizes.XY)]
[GeneratedComputeShaderDescriptor]
public readonly partial struct MedianShaderPass(
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

public sealed class MedianShader(
    int maxNumLayers,
    int pixelWidth,
    int pixelHeight) : IDisposable
{
    GraphicsDevice? _graphicsDevice;
    ReadWriteTexture3D<Rgba32, Float4>? _layers;
    ReadWriteTexture2D<Rgba64, Float4>? _median;
    IMemoryOwner<Rgba64>? _medianLocalCopy;
    ReadWriteTexture2D<Rgba32, Float4>? _brightened;
    int _numLayers;

    public void Dispose()
    {
        _numLayers = 0;
        Interlocked.Exchange(ref _layers, null)?.Dispose();
        Interlocked.Exchange(ref _median, null)?.Dispose();
        Interlocked.Exchange(ref _medianLocalCopy, null)?.Dispose();
        Interlocked.Exchange(ref _brightened, null)?.Dispose();
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
        var median = _median ??= graphicsDevice.AllocateReadWriteTexture2D<Rgba64, Float4>(pixelWidth, pixelHeight, AllocationMode.Clear);
        var medianLocalCopy = _medianLocalCopy ??= MemoryPool<Rgba64>.Shared.Rent(pixelWidth * pixelHeight);
        var medianLocalSpan = medianLocalCopy.Memory.Span[..(pixelWidth * pixelHeight)];
        var brightened = _brightened ??= graphicsDevice.AllocateReadWriteTexture2D<Rgba32, Float4>(pixelWidth, pixelHeight, AllocationMode.Clear);

        // Copy the latest frame into the stack of layers
        var layerIndex = _numLayers++ % maxNumLayers;
        layers.CopyFrom(
            MemoryMarshal.Cast<byte, Rgba32>(layerIn),
            0,
            0,
            layerIndex,
            pixelWidth,
            pixelHeight,
            1);

        // Compute the median of the stack of layers
        graphicsDevice.For(
            pixelWidth,
            pixelHeight,
            new MedianShaderPass(layers, median, Math.Min(maxNumLayers, _numLayers)));

        // Calculate brightness multiplication factor
        median.CopyTo(medianLocalSpan);
        ushort brightestChannel = 0;
        var center = new System.Numerics.Vector2((float)pixelWidth / 2, (float)pixelHeight / 2);
        var maxDistanceFromCenter = Math.Min(pixelWidth, pixelHeight) * 1.0f / 8.0f;
        for (var pixelIndex = 0; pixelIndex < medianLocalSpan.Length; pixelIndex++)
        {
            var pixel = medianLocalSpan[pixelIndex];
            var x = pixelIndex % pixelWidth;
            var y = pixelIndex / pixelWidth;
            var pixelLocation = new System.Numerics.Vector2(x, y);
            if ((pixelLocation - center).Length() > maxDistanceFromCenter)
                continue;
            brightestChannel = Math.Max(
                Math.Max(
                    brightestChannel,
                    pixel.R
                ),
                Math.Max(
                    pixel.G,
                    pixel.B
                )
            );
        }

        var multiplicationFactor = ushort.MaxValue / (float)brightestChannel;

        // Brighten the output image
        graphicsDevice.For(
            pixelWidth,
            pixelHeight,
            new BrightenShader(median, brightened, multiplicationFactor));

        // Copy to output
        brightened.CopyTo(MemoryMarshal.Cast<byte, Rgba32>(medianOut));
    }
}