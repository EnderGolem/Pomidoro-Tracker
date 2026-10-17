using Pomidoro.Domain;
using Pomidoro.Stats;
using Pomidoro.Tests.Fakes;
using Xunit;

namespace Pomidoro.Tests;

public class StatsCalculatorTests
{
    private static readonly DayBoundary Midnight = new(0);
    private static readonly DayRange Week = new(new DateOnly(2026, 10, 12), new DateOnly(2026, 10, 18));
    private static readonly string[] Catalog = ["Работа", "Рисование"];

    private static StatsSummary Build(IReadOnlyList<WorkSession> sessions, params string[] hidden) =>
        StatsCalculator.Build(sessions, Week, Midnight, Catalog, new HashSet<string>(hidden, WorkTypes.Comparer));

    [Fact]
    public void Empty_period_has_no_totals_and_no_median()
    {
        StatsSummary summary = Build([]);

        Assert.Equal(0L, summary.TotalSeconds);
        Assert.Equal(0, summary.Count);
        Assert.Null(summary.MedianDaySeconds);
        Assert.True(summary.IsEmpty);
        Assert.Equal(7, summary.Days.Count);
    }

    [Fact]
    public void Days_without_work_are_still_in_the_list()
    {
        StatsSummary summary = Build([TestSessions.At(2026, 10, 14, 10, 30, "Работа")]);

        Assert.Equal(7, summary.Days.Count);
        Assert.Equal(30 * 60, summary.Days.Single(day => day.Day == new DateOnly(2026, 10, 14)).Seconds);
        Assert.Equal(0L, summary.Days.Single(day => day.Day == new DateOnly(2026, 10, 15)).Seconds);
    }

    // Главный случай: справочник сравнивает имена без учёта регистра, значит и статистика
    // обязана считать «Работа» и «работа» одним типом, а не двумя.
    [Fact]
    public void Same_type_written_in_different_case_stays_one_type()
    {
        StatsSummary summary = Build(
        [
            TestSessions.At(2026, 10, 14, 10, 30, "Работа"),
            TestSessions.At(2026, 10, 14, 12, 20, "работа"),
            TestSessions.At(2026, 10, 15, 10, 10, "РАБОТА")
        ]);

        TypeTotal single = Assert.Single(summary.AllTypes);

        Assert.Equal("Работа", single.Type);
        Assert.Equal(60 * 60, single.Seconds);
        Assert.Equal(50 * 60, summary.Days.Single(day => day.Day == new DateOnly(2026, 10, 14)).Seconds);
    }

    [Fact]
    public void Hiding_a_type_hides_all_its_spellings()
    {
        StatsSummary summary = Build(
            [
                TestSessions.At(2026, 10, 14, 10, 30, "Работа"),
                TestSessions.At(2026, 10, 14, 12, 20, "работа"),
                TestSessions.At(2026, 10, 14, 14, 10, "Рисование")
            ],
            "РАБОТА");

        Assert.Equal(10 * 60, summary.TotalSeconds);
        Assert.Equal(1, summary.Count);

        // В легенде выключенный тип остаётся, иначе его нечем было бы включить обратно.
        Assert.Equal(2, summary.AllTypes.Count);
    }

    [Fact]
    public void Type_named_like_the_no_type_label_does_not_merge_with_empty_ones()
    {
        StatsSummary summary = StatsCalculator.Build(
            [
                TestSessions.At(2026, 10, 14, 10, 30),
                TestSessions.At(2026, 10, 14, 12, 20, "Без типа")
            ],
            Week,
            Midnight,
            ["Без типа"],
            WorkTypes.NewSet());

        Assert.Equal(2, summary.AllTypes.Count);
        Assert.Equal(30 * 60, summary.AllTypes.Single(total => total.Type.Length == 0).Seconds);
        Assert.Equal(20 * 60, summary.AllTypes.Single(total => total.Type.Length > 0).Seconds);
    }

    [Fact]
    public void Types_are_ordered_by_the_catalog_then_alphabetically_with_no_type_last()
    {
        StatsSummary summary = Build(
        [
            TestSessions.At(2026, 10, 14, 10, 10),
            TestSessions.At(2026, 10, 14, 11, 10, "Созвон"),
            TestSessions.At(2026, 10, 14, 12, 10, "Рисование"),
            TestSessions.At(2026, 10, 14, 13, 10, "Архив"),
            TestSessions.At(2026, 10, 14, 14, 10, "Работа")
        ]);

        Assert.Equal(
            ["Работа", "Рисование", "Архив", "Созвон", string.Empty],
            summary.AllTypes.Select(total => total.Type).ToArray());
    }

    [Fact]
    public void Day_segments_go_from_the_longest_type_to_the_shortest()
    {
        StatsSummary summary = Build(
        [
            TestSessions.At(2026, 10, 14, 10, 10, "Работа"),
            TestSessions.At(2026, 10, 14, 12, 40, "Рисование")
        ]);

        DayTotal day = summary.Days.Single(total => total.Day == new DateOnly(2026, 10, 14));

        Assert.Equal(["Рисование", "Работа"], day.ByType.Select(total => total.Type).ToArray());
    }

    // Медиана считается по отработанным дням: в неделе с двумя рабочими днями
    // пять пустых не должны превращать её в ноль.
    [Fact]
    public void Median_ignores_days_without_work()
    {
        StatsSummary summary = Build(
        [
            TestSessions.At(2026, 10, 12, 10, 60, "Работа"),
            TestSessions.At(2026, 10, 14, 10, 120, "Работа"),
            TestSessions.At(2026, 10, 16, 10, 180, "Работа")
        ]);

        Assert.Equal(120 * 60, summary.MedianDaySeconds);
    }

    [Fact]
    public void Median_of_an_even_number_of_days_is_the_average_of_the_middle_two()
    {
        StatsSummary summary = Build(
        [
            TestSessions.At(2026, 10, 12, 10, 60, "Работа"),
            TestSessions.At(2026, 10, 14, 10, 120, "Работа")
        ]);

        Assert.Equal(90 * 60, summary.MedianDaySeconds);
    }

    [Fact]
    public void Sessions_are_counted_against_the_boundary_day_not_the_calendar_day()
    {
        StatsSummary summary = StatsCalculator.Build(
            [TestSessions.At(new DateTime(2026, 10, 15, 2, 0, 0, DateTimeKind.Local), 60, "Работа")],
            Week,
            new DayBoundary(4),
            Catalog,
            WorkTypes.NewSet());

        Assert.Equal(60 * 60, summary.Days.Single(day => day.Day == new DateOnly(2026, 10, 14)).Seconds);
    }
}
