using System.IO;
using System.Windows.Input;
using System.Windows.Media.Imaging;

namespace WpfApp;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow
{
    public MainWindow()
    {
        InitializeComponent();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Key != Key.S || Image.Source is not BitmapSource bitmapSource)
            return;
        var encoder = new JpegBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmapSource));
        using var file = new FileStream(
            Path.Combine(Path.GetTempPath(), $"{DateTime.Now:yyyy-MM-dd HH-mm-ss}.jpg"),
            FileMode.Create,
            FileAccess.ReadWrite,
            FileShare.Read
        );
        encoder.Save(file);
    }
}