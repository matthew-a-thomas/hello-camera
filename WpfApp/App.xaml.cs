using System.Windows;

namespace WpfApp;

/// <summary>
/// Interaction logic for WpfApp.xaml
/// </summary>
public partial class App
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        new MainWindow
        {
            DataContext = new MainViewModel()
        }.Show();
    }
}