using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace WpfApp;

[MarkupExtensionReturnType(typeof(FrameToImageSourceConverter))]
[ValueConversion(typeof(Frame), typeof(ImageSource))]
public sealed class FrameToImageSourceConverter : MarkupExtension, IValueConverter
{
    (int BufferLength, int Stride, WriteableBitmap WriteableBitmap)? _cache;

    public override object ProvideValue(IServiceProvider serviceProvider) => this;

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not Frame frame)
            return DependencyProperty.UnsetValue;
        var stride = frame.Stride;
        if (_cache is not {} cache || cache.BufferLength != frame.Buffer.Length || cache.Stride != stride)
        {
            _cache = cache = (
                frame.Buffer.Length,
                stride,
                new WriteableBitmap(
                    stride / 3,
                    frame.Buffer.Length / stride,
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
                fixed (byte* pointer = frame.Buffer.Span)
                {
                    writeableBitmap.WritePixels(
                        new Int32Rect(0, 0, writeableBitmap.PixelWidth, writeableBitmap.PixelHeight),
                        (IntPtr)pointer,
                        frame.Buffer.Length,
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