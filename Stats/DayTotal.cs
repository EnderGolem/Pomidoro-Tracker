namespace Pomidoro.Stats;

internal sealed record DayTotal(DateOnly Day, long Seconds, IReadOnlyList<TypeTotal> ByType)
{
    public bool IsWeekend => Day.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday;
}
