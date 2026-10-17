namespace Pomidoro.Mvvm;

// Tick source for the timer. Kept separate from it so that in tests time
// is advanced by hand rather than by waiting for real seconds.
internal interface ITicker
{
    event EventHandler? Tick;

    void Start();

    void Stop();
}
