namespace WpfApp;

using System.Runtime.InteropServices;
using ComputeSharp;

public sealed class MedianShaderPass(
    int maxNumLayers,
    int pixelWidth,
    int pixelHeight) : IDisposable
{
    GraphicsDevice? _graphicsDevice;
    ReadWriteBuffer<uint>? _layers;
    ReadWriteTexture2D<Rgba64, Float4>? _median;
    ReadWriteTexture2D<Rgba32, Float4>? _brightened;
    int _numLayers;

    public void Dispose()
    {
        _numLayers = 0;
        Interlocked.Exchange(ref _layers, null)?.Dispose();
        Interlocked.Exchange(ref _median, null)?.Dispose();
        Interlocked.Exchange(ref _brightened, null)?.Dispose();
        Interlocked.Exchange(ref _graphicsDevice, null)?.Dispose();
    }

    public void Incorporate(
        ReadOnlySpan<byte> layerIn,
        Span<byte> medianOut)
    {
        var numPixelsInLayer = pixelWidth * pixelHeight;
        var numBytesInLayer = numPixelsInLayer * 4;
        if (layerIn.Length != numBytesInLayer || medianOut.Length != numBytesInLayer)
            throw new InvalidOperationException("The layer and/or the median have the wrong number of bytes");
        var graphicsDevice = _graphicsDevice ??= GraphicsDevice.GetDefault();
        var layers = _layers ??= graphicsDevice.AllocateReadWriteBuffer<uint>(numPixelsInLayer * maxNumLayers);
        var median = _median ??=
            graphicsDevice.AllocateReadWriteTexture2D<Rgba64, Float4>(pixelWidth, pixelHeight, AllocationMode.Clear);
        var brightened = _brightened ??=
            graphicsDevice.AllocateReadWriteTexture2D<Rgba32, Float4>(pixelWidth, pixelHeight, AllocationMode.Clear);

        // Copy the latest frame into the stack of layers
        var layerIndex = _numLayers++ % maxNumLayers;
        layers.CopyFrom(
            MemoryMarshal.Cast<byte, uint>(layerIn),
            layerIndex * numPixelsInLayer
        );

        // Compute the median of the stack of layers
        graphicsDevice.For(
            pixelWidth,
            pixelHeight,
            new MedianShader(layers, median, Math.Min(maxNumLayers, _numLayers)));

        // Brighten the output image
        graphicsDevice.For(
            pixelWidth,
            pixelHeight,
            new BrightenShader(median, brightened));

        // Copy to output
        brightened.CopyTo(MemoryMarshal.Cast<byte, Rgba32>(medianOut));
    }
}