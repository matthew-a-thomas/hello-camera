namespace WpfApp;

using System.Runtime.InteropServices;
using ComputeSharp;

public sealed class ShaderPass(
    int maxNumLayers,
    int pixelWidth,
    int pixelHeight) : IDisposable
{
    GraphicsDevice? _graphicsDevice;
    ReadWriteTexture3D<Rgba32, Float4>? _layers;
    ReadWriteTexture2D<Rgba64, Float4>? _aggregate;
    ReadWriteTexture2D<Rgba32, Float4>? _output;
    int _numLayers;

    public void Dispose()
    {
        _numLayers = 0;
        Interlocked.Exchange(ref _layers, null)?.Dispose();
        Interlocked.Exchange(ref _aggregate, null)?.Dispose();
        Interlocked.Exchange(ref _output, null)?.Dispose();
        Interlocked.Exchange(ref _graphicsDevice, null)?.Dispose();
    }

    public void Incorporate(
        ReadOnlySpan<byte> layerIn,
        Span<byte> output)
    {
        var numPixelsInLayer = pixelWidth * pixelHeight;
        var numBytesInLayer = numPixelsInLayer * 4;
        if (layerIn.Length != numBytesInLayer || output.Length != numBytesInLayer)
            throw new InvalidOperationException("The layer and/or the output have the wrong number of bytes");
        var graphicsDevice = _graphicsDevice ??= GraphicsDevice.GetDefault();
        var layers = _layers ??= graphicsDevice.AllocateReadWriteTexture3D<Rgba32, Float4>(pixelWidth, pixelHeight, maxNumLayers, AllocationMode.Clear);
        var aggregate = _aggregate ??=
            graphicsDevice.AllocateReadWriteTexture2D<Rgba64, Float4>(pixelWidth, pixelHeight, AllocationMode.Clear);
        var outputTexture = _output ??=
            graphicsDevice.AllocateReadWriteTexture2D<Rgba32, Float4>(pixelWidth, pixelHeight, AllocationMode.Clear);

        // Copy the latest frame into the stack of layers
        var layerIndex = _numLayers++ % maxNumLayers;
        layers.CopyFrom(
            MemoryMarshal.Cast<byte, Rgba32>(layerIn),
            ..,
            ..,
            layerIndex..(layerIndex + 1)
        );

        // Aggregate layers
        graphicsDevice.For(
            pixelWidth,
            pixelHeight,
            new AggregateShader(layers, aggregate, outputTexture, Math.Min(maxNumLayers, _numLayers)));

        // Copy to output
        outputTexture.CopyTo(MemoryMarshal.Cast<byte, Rgba32>(output));
    }
}