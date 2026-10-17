using Pomidoro.Domain;
using Xunit;

namespace Pomidoro.Tests;

public class DayBoundaryTests
{
    [Fact]
    public void Midnight_boundary_keeps_the_calendar_day()
    {
        DayBoundary boundary = new(0);

        Assert.Equal(new DateOnly(2026, 10, 16), boundary.DayOf(new DateTime(2026, 10, 16, 23, 59, 0)));
        Assert.Equal(new DateOnly(2026, 10, 17), boundary.DayOf(new DateTime(2026, 10, 17, 0, 1, 0)));
    }

    [Fact]
    public void Night_work_before_the_boundary_belongs_to_the_previous_day()
    {
        DayBoundary boundary = new(4);

        Assert.Equal(new DateOnly(2026, 10, 16), boundary.DayOf(new DateTime(2026, 10, 17, 3, 59, 0)));
        Assert.Equal(new DateOnly(2026, 10, 17), boundary.DayOf(new DateTime(2026, 10, 17, 4, 0, 0)));
    }

    [Theory]
    [InlineData(-5, DayBoundary.MinHour)]
    [InlineData(99, DayBoundary.MaxHour)]
    [InlineData(7, 7)]
    public void Hour_outside_the_day_is_clamped(int hour, int expected)
    {
        Assert.Equal(expected, new DayBoundary(hour).Hour);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(4)]
    [InlineData(23)]
    public void Day_borders_round_trip_back_to_the_same_day(int hour)
    {
        DayBoundary boundary = new(hour);
        DateOnly day = new(2026, 10, 16);

        DateTime start = boundary.StartUtc(day).ToLocalTime();
        DateTime end = boundary.EndUtc(day).ToLocalTime().AddSeconds(-1);

        Assert.Equal(day, boundary.DayOf(start));
        Assert.Equal(day, boundary.DayOf(end));
    }

    [Fact]
    public void Day_of_a_fixed_clock_is_the_day_of_its_local_time()
    {
        DayBoundary boundary = new(4);
        FakeTimeAtLocalMidnight time = new();

        Assert.Equal(boundary.DayOf(time.GetLocalNow().DateTime), boundary.Today(time));
    }

    private sealed class FakeTimeAtLocalMidnight : TimeProvider
    {
        public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;

        public override DateTimeOffset GetUtcNow() => new(2026, 10, 17, 2, 30, 0, TimeSpan.Zero);
    }
}
