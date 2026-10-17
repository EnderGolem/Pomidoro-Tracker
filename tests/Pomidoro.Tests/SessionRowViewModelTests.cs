using Pomidoro.Domain;
using Pomidoro.Tests.Fakes;
using Pomidoro.ViewModels;
using Xunit;

namespace Pomidoro.Tests;

public class SessionRowViewModelTests
{
    private static readonly string[] Catalog = ["Работа", "Рисование"];

    private readonly FakeSessionStore _store = new();

    [Fact]
    public void Row_shows_the_time_and_the_minutes_of_its_session()
    {
        SessionRowViewModel row = Row(TestSessions.At(2026, 10, 16, 14, 25, "Работа"));

        Assert.Equal("14:00", row.EndTime);
        Assert.Equal("25", row.Minutes);
        Assert.Equal("Работа", row.SelectedOption);
    }

    [Fact]
    public void Date_is_shown_only_where_it_is_asked_for()
    {
        Assert.Equal(string.Empty, Row(TestSessions.At(2026, 10, 16, 14, 25)).Date);
        Assert.Equal("16.10", Row(TestSessions.At(2026, 10, 16, 14, 25), showDate: true).Date);
    }

    // Регрессия: раньше строка с типом, записанным в другом регистре, молча
    // переписывала историю при первой же отрисовке списка.
    [Fact]
    public void Showing_a_row_never_writes_to_the_store()
    {
        Row(TestSessions.At(2026, 10, 16, 14, 25, "работа"));
        Row(TestSessions.At(2026, 10, 16, 15, 25));
        Row(TestSessions.At(2026, 10, 16, 16, 25, "Созвон"));

        Assert.Empty(_store.UpdatedTypes);
    }

    [Fact]
    public void Type_written_in_another_case_is_shown_as_it_is_spelled_in_the_catalog()
    {
        SessionRowViewModel row = Row(TestSessions.At(2026, 10, 16, 14, 25, "работа"));

        Assert.Equal("Работа", row.Type);
        Assert.Equal("Работа", row.SelectedOption);
    }

    [Fact]
    public void Type_that_is_no_longer_in_the_catalog_is_still_offered_in_the_list()
    {
        SessionRowViewModel row = Row(TestSessions.At(2026, 10, 16, 14, 25, "Созвон"));

        Assert.Contains("Созвон", row.TypeOptions);
        Assert.Equal("Созвон", row.SelectedOption);
    }

    [Fact]
    public void Missing_type_is_shown_as_a_dash()
    {
        SessionRowViewModel row = Row(TestSessions.At(2026, 10, 16, 14, 25));

        Assert.Equal(WorkTypes.NoTypeOption, row.SelectedOption);
        Assert.Equal(WorkTypes.NoTypeOption, row.TypeOptions[0]);
    }

    [Fact]
    public void Chosen_type_is_saved()
    {
        SessionRowViewModel row = Row(TestSessions.At(2026, 10, 16, 14, 25));

        row.SelectedOption = "Рисование";

        Assert.Equal("Рисование", row.Type);
        Assert.Equal(["Рисование"], _store.UpdatedTypes);
    }

    [Fact]
    public void Dash_clears_the_type()
    {
        SessionRowViewModel row = Row(TestSessions.At(2026, 10, 16, 14, 25, "Работа"));

        row.SelectedOption = WorkTypes.NoTypeOption;

        Assert.Equal(string.Empty, row.Type);
        Assert.Equal([string.Empty], _store.UpdatedTypes);
    }

    [Fact]
    public void New_duration_is_saved_and_normalized()
    {
        SessionRowViewModel row = Row(TestSessions.At(2026, 10, 16, 14, 25));
        int changed = 0;
        row.Changed += (_, _) => changed++;

        row.Minutes = " 040 ";

        Assert.Equal("40", row.Minutes);
        Assert.Equal(40 * 60, row.DurationSec);
        Assert.Equal(1, changed);
    }

    [Theory]
    [InlineData("")]
    [InlineData("abc")]
    [InlineData("-5")]
    [InlineData("1441")]
    public void Garbage_rolls_back_to_the_previous_value(string typed)
    {
        SessionRowViewModel row = Row(TestSessions.At(2026, 10, 16, 14, 25));

        row.Minutes = typed;

        Assert.Equal("25", row.Minutes);
        Assert.Equal(25 * 60, row.DurationSec);
    }

    [Fact]
    public void Zero_minutes_is_allowed()
    {
        SessionRowViewModel row = Row(TestSessions.At(2026, 10, 16, 14, 25));

        row.Minutes = "0";

        Assert.Equal(0, row.DurationSec);
    }

    [Fact]
    public void Delete_command_asks_the_owner_instead_of_deleting_by_itself()
    {
        SessionRowViewModel row = Row(TestSessions.At(2026, 10, 16, 14, 25));
        int asked = 0;
        row.DeleteRequested += (_, _) => asked++;

        row.DeleteCommand.Execute(null);

        Assert.Equal(1, asked);
        Assert.Empty(_store.DeletedIds);
    }

    private SessionRowViewModel Row(WorkSession session, bool showDate = false) =>
        new(session with { Id = 1 }, Catalog, showDate, _store);
}
