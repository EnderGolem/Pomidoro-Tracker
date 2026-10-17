using System.Globalization;
using Pomidoro.Domain;
using Pomidoro.Formatting;
using Xunit;

namespace Pomidoro.Tests;

public class FormatTests
{
    [Theory]
    [InlineData(0, "00:00")]
    [InlineData(59, "00:59")]
    [InlineData(25 * 60, "25:00")]
    [InlineData(60 * 60, "1:00:00")]
    [InlineData(2 * 60 * 60 + 5 * 60 + 7, "2:05:07")]
    public void Clock_shows_hours_only_when_there_are_any(int seconds, string expected)
    {
        Assert.Equal(expected, Format.Clock(TimeSpan.FromSeconds(seconds)));
    }

    [Fact]
    public void Clock_rounds_seconds_up_so_the_start_shows_a_whole_minute()
    {
        Assert.Equal("25:00", Format.Clock(TimeSpan.FromSeconds(1499.6)));
    }

    [Theory]
    [InlineData(0, "0.0")]
    [InlineData(1800, "0.5")]
    [InlineData(66967, "18.6")]
    public void Hours_are_shown_as_one_number(long seconds, string expected)
    {
        Assert.Equal(expected, Format.HoursNumber(seconds));
    }

    [Fact]
    public void Range_of_one_day_is_shown_as_a_single_date()
    {
        Assert.Equal("16.10.2026", Format.Range(DayRange.SingleDay(new DateOnly(2026, 10, 16))));
    }

    [Fact]
    public void Range_of_several_days_shows_both_ends()
    {
        Assert.Equal(
            "12.10 — 16.10.2026",
            Format.Range(new DayRange(new DateOnly(2026, 10, 12), new DateOnly(2026, 10, 16))));
    }

    [Theory]
    [InlineData(0.25, 0.25, "0.25")]
    [InlineData(1.5, 0.5, "1.5")]
    [InlineData(6.0, 2.0, "6")]
    public void Axis_label_keeps_only_the_digits_it_needs(double hours, double step, string expected)
    {
        Assert.Equal(expected, Format.AxisHours(hours, step));
    }

    // Формат не должен зависеть от локали машины: на ru-RU разделитель дробной части — запятая.
    [Fact]
    public void Numbers_do_not_follow_the_machine_locale()
    {
        CultureInfo original = CultureInfo.CurrentCulture;

        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("ru-RU");

            Assert.Equal("18.6", Format.HoursNumber(66967));
            Assert.Equal("16.10", Format.DayAndMonth(new DateOnly(2026, 10, 16)));
            Assert.Equal("25:00", Format.Clock(TimeSpan.FromMinutes(25)));
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }
}
