namespace Pomidoro.Stats;

internal sealed record StatsSummary(
    long TotalSeconds,
    int Count,
    long? MedianDaySeconds,
    IReadOnlyList<DayTotal> Days,
    IReadOnlyList<TypeTotal> AllTypes)
{
    public long MaxDaySeconds => Days.Count == 0 ? 0L : Days.Max(day => day.Seconds);

    public bool IsEmpty => MaxDaySeconds == 0L;
}
