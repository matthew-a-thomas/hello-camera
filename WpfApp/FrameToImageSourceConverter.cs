using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace WpfApp;

[MarkupExtensionReturnType(typeof(FrameToImageSourceConverter))]
[ValueConversion(typeof(FrameAvailableEvent), typeof(ImageSource))]
public sealed class FrameToImageSourceConverter(int medianFactor) : MarkupExtension, IValueConverter
{
    (int BufferLength, int Stride, WriteableBitmap WriteableBitmap)? _cache;
    MedianShader? _medianShader;

    public override object ProvideValue(IServiceProvider serviceProvider) => this;

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not FrameAvailableEvent frameAvailableEvent)
        {
            _cache = null;
            _medianShader?.Dispose();
            _medianShader = null;
            return DependencyProperty.UnsetValue;
        }
        using var _ = frameAvailableEvent;
        frameAvailableEvent.Increment();
        var stride = frameAvailableEvent.Stride;
        if (_cache is not {} cache || _medianShader is not {} medianShader || cache.BufferLength != frameAvailableEvent.Memory.Length || cache.Stride != stride)
        {
            var pixelWidth = stride / 4;
            var pixelHeight = frameAvailableEvent.Memory.Length / stride;
            if (pixelWidth == 0 || pixelHeight == 0)
            {
                _cache = null;
                _medianShader?.Dispose();
                _medianShader = null;
                return DependencyProperty.UnsetValue;
            }

            _cache = cache = (
                frameAvailableEvent.Memory.Length,
                stride,
                new WriteableBitmap(
                    pixelWidth,
                    pixelHeight,
                    96.0,
                    96.0,
                    PixelFormats.Bgra32,
                    null
                )
            );
            _medianShader?.Dispose();
            _medianShader = medianShader = new MedianShader(medianFactor, pixelWidth, pixelHeight);
        }
        var writeableBitmap = cache.WriteableBitmap;
        writeableBitmap.Lock();
        try
        {
            unsafe
            {
                var backBuffer = new Span<byte>(
                    (void*)writeableBitmap.BackBuffer,
                    writeableBitmap.BackBufferStride * writeableBitmap.PixelHeight
                );
                medianShader.Incorporate(
                    frameAvailableEvent.Memory.Span,
                    backBuffer
                );
                var dirtyRect = new Int32Rect(0, 0, writeableBitmap.PixelWidth, writeableBitmap.PixelHeight);
                writeableBitmap.AddDirtyRect(dirtyRect);
                // fixed (byte* pointer = frameAvailableEvent.Memory.Span)
                // {
                //     writeableBitmap.WritePixels(
                //         dirtyRect,
                //         (IntPtr)pointer,
                //         frameAvailableEvent.Memory.Length,
                //         stride
                //     );
                // }
            }
        }
        finally
        {
            writeableBitmap.Unlock();
        }
        return writeableBitmap;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        DependencyProperty.UnsetValue;
}