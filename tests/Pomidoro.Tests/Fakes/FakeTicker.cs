using Pomidoro.Mvvm;

namespace Pomidoro.Tests.Fakes;

internal sealed class FakeTicker : ITicker
{
    public event EventHandler? Tick;

    public bool IsRunning { get; private set; }

    public void Start() => IsRunning = true;

    public void Stop() => IsRunning = false;

    public void Fire() => Tick?.Invoke(this, EventArgs.Empty);
}
