using System.IO;
using Pomidoro.Domain;
using Pomidoro.Storage;
using Pomidoro.Tests.Fakes;
using Xunit;

namespace Pomidoro.Tests;

// Tests run against real SQLite in a temporary file: the schema, the migration
// and the queries are exactly what breaks when left untested.
public class SqliteSessionStoreTests : IDisposable
{
    private static readonly DayBoundary Midnight = new(0);

    private readonly string _path;
    private readonly SqliteSessionStore _store;

    public SqliteSessionStoreTests()
    {
        _path = Path.Combine(Path.GetTempPath(), "pomidoro-tests", Guid.NewGuid().ToString("N"), "test.db");
        _store = new SqliteSessionStore(_path);
    }

    public void Dispose()
    {
        _store.Dispose();

        string? directory = Path.GetDirectoryName(_path);

        if (directory is not null && Directory.Exists(directory))
        {
            Directory.Delete(directory, recursive: true);
        }

        GC.SuppressFinalize(this);
    }

    [Fact]
    public void Database_file_is_created_next_to_the_given_path()
    {
        Assert.True(File.Exists(_path));
        Assert.Equal(_path, _store.DatabasePath);
    }

    [Fact]
    public void Saved_session_comes_back_with_an_id_and_the_same_values()
    {
        _store.Add(TestSessions.At(2026, 10, 16, 10, 25, "Работа"));

        WorkSession saved = Assert.Single(_store.GetRange(Day(2026, 10, 16), Midnight));

        Assert.True(saved.Id > 0);
        Assert.Equal(25 * 60, saved.DurationSec);
        Assert.Equal("Работа", saved.Type);
        Assert.Equal(DateTimeKind.Utc, saved.EndUtc.Kind);
    }

    [Fact]
    public void Range_is_limited_by_the_boundary_day_on_both_ends()
    {
        _store.Add(TestSessions.At(2026, 10, 15, 23, 10));
        _store.Add(TestSessions.At(2026, 10, 16, 10, 20));
        _store.Add(TestSessions.At(2026, 10, 17, 1, 30));

        Assert.Single(_store.GetRange(Day(2026, 10, 16), Midnight));
        Assert.Equal(3, _store.GetRange(new DayRange(new DateOnly(2026, 10, 15), new DateOnly(2026, 10, 17)), Midnight).Count);
    }

    [Fact]
    public void Night_session_lands_in_the_previous_day_when_the_boundary_is_moved()
    {
        _store.Add(TestSessions.At(2026, 10, 17, 2, 30));

        DayBoundary fourInTheMorning = new(4);

        Assert.Empty(_store.GetRange(Day(2026, 10, 17), fourInTheMorning));
        Assert.Single(_store.GetRange(Day(2026, 10, 16), fourInTheMorning));
    }

    [Fact]
    public void Newest_sessions_come_first()
    {
        _store.Add(TestSessions.At(2026, 10, 16, 9, 10));
        _store.Add(TestSessions.At(2026, 10, 16, 18, 10));
        _store.Add(TestSessions.At(2026, 10, 16, 13, 10));

        IReadOnlyList<WorkSession> sessions = _store.GetRange(Day(2026, 10, 16), Midnight);

        Assert.True(sessions[0].EndUtc > sessions[1].EndUtc);
        Assert.True(sessions[1].EndUtc > sessions[2].EndUtc);
    }

    [Fact]
    public void Duration_and_type_can_be_changed_afterwards()
    {
        _store.Add(TestSessions.At(2026, 10, 16, 10, 25, "Работа"));
        long id = _store.GetRange(Day(2026, 10, 16), Midnight)[0].Id;

        _store.UpdateDuration(id, 40 * 60);
        _store.UpdateType(id, "Рисование");

        WorkSession updated = _store.GetRange(Day(2026, 10, 16), Midnight)[0];

        Assert.Equal(40 * 60, updated.DurationSec);
        Assert.Equal("Рисование", updated.Type);
    }

    [Fact]
    public void Deleted_session_is_gone()
    {
        _store.Add(TestSessions.At(2026, 10, 16, 10, 25));
        _store.Delete(_store.GetRange(Day(2026, 10, 16), Midnight)[0].Id);

        Assert.Empty(_store.GetRange(Day(2026, 10, 16), Midnight));
    }

    // SQLite's built-in NOCASE folds only Latin letters, so case is
    // compared in C#. Without that, "созвон" and "СОЗВОН" would become different types.
    [Theory]
    [InlineData("созвон")]
    [InlineData("СОЗВОН")]
    [InlineData("  Созвон  ")]
    public void Catalog_does_not_take_the_same_name_twice(string duplicate)
    {
        _store.AddTypeToCatalog("Созвон");
        _store.AddTypeToCatalog(duplicate);

        Assert.Equal(["Созвон"], _store.GetTypeCatalog());
    }

    [Fact]
    public void Catalog_keeps_the_order_in_which_types_were_added()
    {
        _store.AddTypeToCatalog("Работа");
        _store.AddTypeToCatalog("Рисование");
        _store.AddTypeToCatalog("Личное");

        Assert.Equal(["Работа", "Рисование", "Личное"], _store.GetTypeCatalog());
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("Без типа")]
    [InlineData("No type")]
    [InlineData(WorkTypes.NoTypeOption)]
    public void Empty_and_service_names_do_not_get_into_the_catalog(string name)
    {
        _store.AddTypeToCatalog(name);

        Assert.Empty(_store.GetTypeCatalog());
    }

    [Fact]
    public void Removing_a_type_from_the_catalog_keeps_it_in_the_history()
    {
        _store.AddTypeToCatalog("Созвон");
        _store.Add(TestSessions.At(2026, 10, 16, 10, 25, "Созвон"));

        _store.RemoveTypeFromCatalog("СОЗВОН");

        Assert.Empty(_store.GetTypeCatalog());
        Assert.Equal("Созвон", _store.GetRange(Day(2026, 10, 16), Midnight)[0].Type);
    }

    [Fact]
    public void Setting_falls_back_until_it_is_saved()
    {
        Assert.Equal(25, _store.GetSetting("WorkMinutes", 25));

        _store.SetSetting("WorkMinutes", 50);

        Assert.Equal(50, _store.GetSetting("WorkMinutes", 25));
    }

    [Fact]
    public void Settings_survive_reopening_the_file()
    {
        _store.SetSetting("DayBoundaryHour", 4);
        _store.AddTypeToCatalog("Работа");
        _store.Add(TestSessions.At(2026, 10, 16, 10, 25, "Работа"));
        _store.Dispose();

        using SqliteSessionStore reopened = new(_path);

        Assert.Equal(4, reopened.GetSetting("DayBoundaryHour", 0));
        Assert.Equal(["Работа"], reopened.GetTypeCatalog());
        Assert.Single(reopened.GetRange(Day(2026, 10, 16), Midnight));
    }

    [Fact]
    public void Sql_console_returns_rows_of_a_select()
    {
        _store.Add(TestSessions.At(2026, 10, 16, 10, 25, "Работа"));

        SqlQueryResult result = _store.Execute("SELECT Id, Type FROM WorkSessions;");

        Assert.False(result.Failed);
        Assert.Equal(["Id", "Type"], Assert.Single(result.Sets).Columns);
        Assert.Equal(1, result.RowCount);
    }

    [Fact]
    public void Sql_console_reports_a_broken_query_instead_of_throwing()
    {
        SqlQueryResult result = _store.Execute("SELEKT oops;");

        Assert.True(result.Failed);
        Assert.Contains("SELEKT", result.Error, StringComparison.Ordinal);
    }

    [Fact]
    public void Sql_console_counts_changed_rows_and_hides_the_minus_one_of_a_select()
    {
        _store.Add(TestSessions.At(2026, 10, 16, 10, 25, "Работа"));

        Assert.Equal(1, _store.Execute("UPDATE WorkSessions SET Type = 'Личное';").ChangedRows);
        Assert.Equal(0, _store.Execute("SELECT * FROM WorkSessions;").ChangedRows);
    }

    [Fact]
    public void Sql_console_shows_nulls_and_blobs_readably()
    {
        SqlResultSet set = Assert.Single(_store.Execute("SELECT NULL AS n, x'00ff' AS b, 1.5 AS d;").Sets);

        Assert.Equal(["NULL", "BLOB[2]", "1.5"], set.Rows[0]);
    }

    private static DayRange Day(int year, int month, int day) => DayRange.SingleDay(new DateOnly(year, month, day));
}
