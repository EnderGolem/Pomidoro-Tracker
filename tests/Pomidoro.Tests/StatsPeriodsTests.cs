using Pomidoro.Domain;
using Xunit;

namespace Pomidoro.Tests;

public class StatsPeriodsTests
{
    // 16.10.2026 — пятница.
    private static readonly DateOnly Friday = new(2026, 10, 16);

    [Fact]
    public void Current_week_starts_on_monday_and_ends_today()
    {
        DayRange range = StatsPeriods.Resolve(StatsPeriod.CurrentWeek, Friday);

        Assert.Equal(new DateOnly(2026, 10, 12), range.From);
        Assert.Equal(Friday, range.To);
        Assert.Equal(5, range.Days);
    }

    [Fact]
    public void Previous_week_is_a_full_seven_days()
    {
        DayRange range = StatsPeriods.Resolve(StatsPeriod.PreviousWeek, Friday);

        Assert.Equal(new DateOnly(2026, 10, 5), range.From);
        Assert.Equal(new DateOnly(2026, 10, 11), range.To);
        Assert.Equal(7, range.Days);
    }

    [Fact]
    public void Week_of_a_sunday_still_starts_on_monday()
    {
        DayRange range = StatsPeriods.Resolve(StatsPeriod.CurrentWeek, new DateOnly(2026, 10, 18));

        Assert.Equal(new DateOnly(2026, 10, 12), range.From);
    }

    [Fact]
    public void Current_month_starts_on_the_first_and_ends_today()
    {
        DayRange range = StatsPeriods.Resolve(StatsPeriod.CurrentMonth, Friday);

        Assert.Equal(new DateOnly(2026, 10, 1), range.From);
        Assert.Equal(Friday, range.To);
    }

    [Fact]
    public void Previous_month_covers_it_whole()
    {
        DayRange range = StatsPeriods.Resolve(StatsPeriod.PreviousMonth, Friday);

        Assert.Equal(new DateOnly(2026, 9, 1), range.From);
        Assert.Equal(new DateOnly(2026, 9, 30), range.To);
        Assert.Equal(30, range.Days);
    }

    [Fact]
    public void Range_enumerates_every_day_including_both_ends()
    {
        DayRange range = new(new DateOnly(2026, 10, 14), new DateOnly(2026, 10, 16));

        Assert.Equal(
            [new DateOnly(2026, 10, 14), new DateOnly(2026, 10, 15), new DateOnly(2026, 10, 16)],
            range.EnumerateDays().ToArray());
    }
}
