using System.ComponentModel;
using System.Reactive;
using System.Reactive.Linq;

namespace WpfApp;

public sealed class MainViewModel : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    public MainViewModel()
    {
        Camera
            .GetFrames(0)
            .Select(x => (Frame?)x)
            .Materialize()
            .SelectMany(notification => notification.Kind == NotificationKind.OnNext
                ? Observable.Return(notification)
                : Observable.Return(Notification.CreateOnNext<Frame?>(null)).Append(notification))
            .Dematerialize()
            .RetryWhen(e => e.Delay(TimeSpan.FromSeconds(1)))
            .Subscribe(frame => Frame = frame);
    }

    Frame? _frame;
    public Frame? Frame
    {
        get => _frame;
        private set
        {
            if (_frame == value)
                return;
            _frame = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Frame)));
        }
    }
}