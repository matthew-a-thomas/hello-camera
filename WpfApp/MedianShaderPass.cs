namespace WpfApp;

using System.Buffers;
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
        var numPixelsInLayer = pixelWidth * pixelHeight;
        var numBytesInLayer = numPixelsInLayer * 4;
        if (layerIn.Length != numBytesInLayer || medianOut.Length != numBytesInLayer)
            throw new InvalidOperationException("The layer and/or the median have the wrong number of bytes");
        var graphicsDevice = _graphicsDevice ??= GraphicsDevice.GetDefault();
        var layers = _layers ??= graphicsDevice.AllocateReadWriteBuffer<uint>(numPixelsInLayer * maxNumLayers);
        var median = _median ??=
            graphicsDevice.AllocateReadWriteTexture2D<Rgba64, Float4>(pixelWidth, pixelHeight, AllocationMode.Clear);
        var medianLocalCopy = _medianLocalCopy ??= MemoryPool<Rgba64>.Shared.Rent(numPixelsInLayer);
        var medianLocalSpan = medianLocalCopy.Memory.Span[..numPixelsInLayer];
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

        // Calculate brightness multiplication factor
        median.CopyTo(medianLocalSpan);
        var highestPerceivedBrightness = 0.0;
        var center = new System.Numerics.Vector2((float)pixelWidth / 2, (float)pixelHeight / 2);
        var maxDistanceFromCenter = Math.Min(pixelWidth, pixelHeight) * 1.0f / 10.0f;
        for (var pixelIndex = 0; pixelIndex < medianLocalSpan.Length; pixelIndex++)
        {
            var pixel = medianLocalSpan[pixelIndex];
            var x = pixelIndex % pixelWidth;
            var y = pixelIndex / pixelWidth;
            var pixelLocation = new System.Numerics.Vector2(x, y);
            if ((pixelLocation - center).Length() > maxDistanceFromCenter)
                continue;

            highestPerceivedBrightness = Math.Max(highestPerceivedBrightness, PerceivedBrightness(pixel));
        }

        var multiplicationFactor = (float)(100.0 / highestPerceivedBrightness);
        Console.WriteLine(multiplicationFactor);

        // Brighten the output image
        graphicsDevice.For(
            pixelWidth,
            pixelHeight,
            new BrightenShader(median, brightened, multiplicationFactor));

        // Copy to output
        brightened.CopyTo(MemoryMarshal.Cast<byte, Rgba32>(medianOut));
    }

    static double Luminance(Rgba64 pixel)
    {
        var rLin = Linearize(pixel.R);
        var gLin = Linearize(pixel.G);
        var bLin = Linearize(pixel.B);

        return 0.2126 * rLin + 0.7152 * gLin + 0.0722 * bLin;
    }

    static double Linearize(ushort colorChannel)
    {
        var gamma = colorChannel / (double)ushort.MaxValue;
        if (gamma <= 0.04045)
        {
            return gamma / 12.02;
        }

        return Math.Pow((gamma + 0.055) / 1.055, 2.4);
    }

    /// <returns>A number between 0 and 100</returns>
    static double PerceivedBrightness(Rgba64 pixel)
    {
        // https://stackoverflow.com/a/56678483/3063273
        var luminance = Luminance(pixel);
        if (luminance <= 216.0 / 24389.0)
        {
            // The CIE standard states 0.008856 but 216/24389 is the intent for 0.008856451679036
            // The CIE standard states 903.3, but 24389/27 is the intent, making 903.296296296296296
            return luminance * (24389.0 / 27.0);
        }
        else
        {
            return Math.Pow(luminance, 1.0 / 3.0) * 116 - 16;
        }
    }
}