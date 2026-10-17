namespace Pomidoro.Domain;

internal readonly record struct DayRange(DateOnly From, DateOnly To)
{
    public static DayRange SingleDay(DateOnly day) => new(day, day);

    public bool IsSingleDay => From == To;

    public int Days => To.DayNumber - From.DayNumber + 1;

    public bool Contains(DateOnly day) => day >= From && day <= To;

    public IEnumerable<DateOnly> EnumerateDays()
    {
        for (DateOnly day = From; day <= To; day = day.AddDays(1))
        {
            yield return day;
        }
    }
}
