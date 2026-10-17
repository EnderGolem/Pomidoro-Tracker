using System.Windows.Threading;

namespace Pomidoro.Mvvm;

internal sealed class DispatcherTicker : ITicker
{
    private readonly DispatcherTimer _timer;

    public DispatcherTicker(TimeSpan interval)
    {
        _timer = new DispatcherTimer(DispatcherPriority.Normal) { Interval = interval };
        _timer.Tick += (_, _) => Tick?.Invoke(this, EventArgs.Empty);
    }

    public event EventHandler? Tick;

    public void Start() => _timer.Start();

    public void Stop() => _timer.Stop();
}
