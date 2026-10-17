using Pomidoro.Localization;
using Pomidoro.Storage;
using Pomidoro.Tests.Fakes;
using Pomidoro.ViewModels;
using Xunit;

namespace Pomidoro.Tests;

// Поведение легенды задано словами: обычный клик оставляет только нажатый тип,
// повторный клик по единственному включённому возвращает все, Shift+клик
// добавляет или убирает один.
public class StatsViewModelTests
{
    private readonly FakeSessionStore _store = new();
    private readonly StatsViewModel _stats;

    public StatsViewModelTests()
    {
        _store.AddTypeToCatalog("Работа");
        _store.AddTypeToCatalog("Рисование");

        _store.AddRange(
            TestSessions.At(2026, 10, 14, 10, 60, "Работа"),
            TestSessions.At(2026, 10, 15, 10, 30, "Рисование"),
            TestSessions.At(2026, 10, 16, 10, 20));

        FakeTime time = new(new DateTimeOffset(2026, 10, 16, 12, 0, 0, TimeSpan.Zero));
        AppSettings settings = new(_store);

        _stats = new StatsViewModel(_store, settings, time);
        _stats.Reload();
    }

    [Fact]
    public void Period_opens_on_the_current_week_with_everything_shown()
    {
        Assert.Equal("12.10 — 16.10.2026", _stats.RangeText);
        Assert.Equal(Loc.S.Hours("1.8"), _stats.HoursText);
        Assert.Equal(Loc.S.Pomodoros(3), _stats.CountText);
        Assert.Equal(["Работа", "Рисование", string.Empty], Types());
        Assert.All(_stats.Legend, item => Assert.True(item.IsShown));
    }

    [Fact]
    public void Median_counts_only_the_days_that_were_worked()
    {
        Assert.Equal(Loc.S.Median("0.5"), _stats.MedianText);
    }

    [Fact]
    public void Click_leaves_only_the_type_that_was_clicked()
    {
        _stats.ToggleType("Работа", shiftHeld: false);

        Assert.Equal([true, false, false], Shown());
        Assert.Equal(Loc.S.Hours("1.0"), _stats.HoursText);
        Assert.Single(_stats.List.Rows);
    }

    [Fact]
    public void Click_on_the_only_shown_type_brings_everything_back()
    {
        _stats.ToggleType("Работа", shiftHeld: false);
        _stats.ToggleType("Работа", shiftHeld: false);

        Assert.Equal([true, true, true], Shown());
        Assert.Equal(Loc.S.Hours("1.8"), _stats.HoursText);
    }

    [Fact]
    public void Click_on_another_type_switches_to_it()
    {
        _stats.ToggleType("Работа", shiftHeld: false);
        _stats.ToggleType("Рисование", shiftHeld: false);

        Assert.Equal([false, true, false], Shown());
    }

    [Fact]
    public void Shift_click_removes_and_returns_a_single_type()
    {
        _stats.ToggleType("Рисование", shiftHeld: true);

        Assert.Equal([true, false, true], Shown());
        Assert.Equal(Loc.S.Hours("1.3"), _stats.HoursText);

        _stats.ToggleType("Рисование", shiftHeld: true);

        Assert.Equal([true, true, true], Shown());
    }

    [Fact]
    public void Shift_click_on_the_last_shown_type_hides_everything()
    {
        _stats.ToggleType("Работа", shiftHeld: false);
        _stats.ToggleType("Работа", shiftHeld: true);

        Assert.Equal([false, false, false], Shown());
        Assert.Equal(Loc.S.Hours("0.0"), _stats.HoursText);
        Assert.Equal(Loc.S.Pomodoros(0), _stats.CountText);
        Assert.True(_stats.IsEmpty);
    }

    [Fact]
    public void Type_written_in_another_case_does_not_add_a_second_legend_item()
    {
        _store.AddRange(TestSessions.At(2026, 10, 16, 15, 40, "работа"));
        _stats.Reload();

        Assert.Equal(["Работа", "Рисование", string.Empty], Types());
        Assert.Equal(Loc.S.Hours("1.7"), _stats.Legend[0].Hours);
    }

    // ComboBox сбрасывает выбор в null, когда теряет выбранный элемент из списка.
    // Раньше это роняло экран: Reload() шёл за Period у пустого периода.
    [Fact]
    public void Period_cannot_be_dropped_to_nothing()
    {
        PeriodOption before = _stats.SelectedPeriod;
        int columns = _stats.Columns.Count;

        _stats.SelectedPeriod = null!;

        Assert.Same(before, _stats.SelectedPeriod);
        Assert.Equal(columns, _stats.Columns.Count);
    }

    [Fact]
    public void Missing_type_is_shown_as_no_type_in_the_legend()
    {
        Assert.Equal(Loc.S.NoType, _stats.Legend[^1].Display);
    }

    [Fact]
    public void Chart_has_a_column_for_every_day_of_the_period()
    {
        _stats.SetPlotSize(400.0, 190.0);

        Assert.Equal(5, _stats.Columns.Count);
        Assert.Equal(["12", "13", "14", "15", "16"], _stats.Columns.Select(column => column.Label).ToArray());
        Assert.NotEmpty(_stats.GridLines);
    }

    [Fact]
    public void Resizing_the_panel_only_changes_the_geometry()
    {
        _stats.SetPlotSize(400.0, 190.0);
        double wide = _stats.Columns[0].Width;

        _stats.SetPlotSize(120.0, 190.0);

        Assert.True(_stats.Columns[0].Width < wide);
        Assert.Equal(Loc.S.Hours("1.8"), _stats.HoursText);
        Assert.Equal(3, _stats.List.Rows.Count);
    }

    [Fact]
    public void Deleting_a_row_removes_it_from_the_history_and_from_the_totals()
    {
        _stats.List.Rows[0].DeleteCommand.Execute(null);

        Assert.Single(_store.DeletedIds);
        Assert.Equal(2, _stats.List.Rows.Count);
        Assert.Equal(Loc.S.Hours("1.5"), _stats.HoursText);
    }

    [Fact]
    public void Editing_minutes_updates_the_totals_without_rebuilding_the_list()
    {
        SessionRowViewModel first = _stats.List.Rows[0];

        first.Minutes = "40";

        Assert.Same(first, _stats.List.Rows[0]);
        Assert.Equal(Loc.S.Hours("2.2"), _stats.HoursText);
    }

    private string[] Types() => _stats.Legend.Select(item => item.Type).ToArray();

    private bool[] Shown() => _stats.Legend.Select(item => item.IsShown).ToArray();
}
