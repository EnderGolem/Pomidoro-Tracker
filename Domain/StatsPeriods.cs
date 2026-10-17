namespace Pomidoro.Domain;

internal static class StatsPeriods
{
    public static readonly IReadOnlyList<StatsPeriod> All = Enum.GetValues<StatsPeriod>();

    public static DayRange Resolve(StatsPeriod period, DateOnly today) => period switch
    {
        StatsPeriod.CurrentWeek => new DayRange(WeekStart(today), today),
        StatsPeriod.PreviousWeek => new DayRange(WeekStart(today).AddDays(-7), WeekStart(today).AddDays(-1)),
        StatsPeriod.CurrentMonth => new DayRange(MonthStart(today), today),
        StatsPeriod.PreviousMonth => new DayRange(MonthStart(today).AddMonths(-1), MonthStart(today).AddDays(-1)),
        _ => DayRange.SingleDay(today)
    };
    // Неделя начинается с понедельника, а не с воскресенья, как по умолчанию в DayOfWeek.
    private static DateOnly WeekStart(DateOnly day) => day.AddDays(-(((int)day.DayOfWeek + 6) % 7));

    private static DateOnly MonthStart(DateOnly day) => new(day.Year, day.Month, 1);
}
