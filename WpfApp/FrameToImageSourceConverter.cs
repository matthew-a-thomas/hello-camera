using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace WpfApp;

[MarkupExtensionReturnType(typeof(FrameToImageSourceConverter))]
[ValueConversion(typeof(FrameAvailableEvent), typeof(ImageSource))]
public sealed class FrameToImageSourceConverter : MarkupExtension, IValueConverter
{
    (int BufferLength, int Stride, WriteableBitmap WriteableBitmap)? _cache;

    public override object ProvideValue(IServiceProvider serviceProvider) => this;

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not FrameAvailableEvent frameAvailableEvent)
            return DependencyProperty.UnsetValue;
        using var _ = frameAvailableEvent;
        frameAvailableEvent.Increment();
        var stride = frameAvailableEvent.Stride;
        if (_cache is not {} cache || cache.BufferLength != frameAvailableEvent.Memory.Length || cache.Stride != stride)
        {
            _cache = cache = (
                frameAvailableEvent.Memory.Length,
                stride,
                new WriteableBitmap(
                    stride / 3,
                    frameAvailableEvent.Memory.Length / stride,
                    96.0,
                    96.0,
                    PixelFormats.Bgr24,
                    null
                )
            );
        }
        var writeableBitmap = cache.WriteableBitmap;
        writeableBitmap.Lock();
        try
        {
            unsafe
            {
                fixed (byte* pointer = frameAvailableEvent.Memory.Span)
                {
                    writeableBitmap.WritePixels(
                        new Int32Rect(0, 0, writeableBitmap.PixelWidth, writeableBitmap.PixelHeight),
                        (IntPtr)pointer,
                        frameAvailableEvent.Memory.Length,
                        stride
                    );
                }
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