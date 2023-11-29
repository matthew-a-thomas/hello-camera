using System.ComponentModel;
using System.Reactive;
using System.Reactive.Linq;
using System.Runtime.CompilerServices;

namespace WpfApp;

public sealed class MainViewModel : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    public MainViewModel()
    {
        Camera
            .GetFrames(0)
            .Select(x => (FrameAvailableEvent?)x)
            .Materialize()
            .SelectMany(notification => notification.Kind == NotificationKind.OnNext
                ? Observable.Return(notification)
                : Observable.Return(Notification.CreateOnNext<FrameAvailableEvent?>(null)).Append(notification))
            .Dematerialize()
            .RetryWhen(e => e.Delay(TimeSpan.FromSeconds(1)))
            .Subscribe(frame => Frame = frame);
    }

    string? _fps;
    public string? Fps
    {
        get => _fps;
        private set
        {
            if (_fps == value)
                return;
            _fps = value;
            RaisePropertyChanged();
        }
    }

    FrameAvailableEvent? _frame;
    public FrameAvailableEvent? Frame
    {
        get => _frame;
        private set
        {
            if (_frame == value)
                return;
            if (value is null || _frame is null)
            {
                Fps = null;
            }
            else
            {
                var elapsed = value.Timestamp - _frame.Timestamp;
                Fps = $"{1.0 / elapsed.TotalSeconds:N1}";
            }
            value?.Increment();
            Interlocked.Exchange(ref _frame, value)?.Dispose();
            RaisePropertyChanged();
        }
    }

    void RaisePropertyChanged([CallerMemberName] string name = "")
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}