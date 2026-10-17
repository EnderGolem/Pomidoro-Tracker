namespace Pomidoro.Domain;

internal readonly record struct TimerStep(TimerEvent Event, WorkSession? Completed)
{
    public static TimerStep Nothing => new(TimerEvent.Nothing, null);
}
