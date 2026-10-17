# Tests

```
dotnet test tests\Pomidoro.Tests\Pomidoro.Tests.csproj -c Debug
```

There are 162 tests right now, and they run in a second and a half. None of them waits
for real time, touches the user's database or opens a window.

## What is covered

| File | Tests | What about |
|---|---|---|
| `PomodoroTimerTests` | 11 | full cycle, pauses, early stop, edge cases |
| `DayBoundaryTests` | 9 | logical day, carrying night work over, clamping the hour |
| `StatsPeriodsTests` | 6 | weeks starting on Monday, months, enumerating days |
| `WorkTypesTests` | 14 | case normalization, empty type, reserved names in both languages |
| `StatsCalculatorTests` | 10 | totals, days, type order, median, case |
| `ChartLayoutTests` | 18 | grid step, axis ceiling, widths, heights |
| `FormatTests` | 15 | clock face, numbers, dates, independence from the locale |
| `StringsTests` | 24 | completeness of both languages, plural forms, markup keys |
| `SqliteSessionStoreTests` | 23 | schema, queries, edits, catalog, settings, console |
| `SessionRowViewModelTests` | 15 | editing minutes and type, rolling back garbage, no extra writes |
| `StatsViewModelTests` | 14 | legend, period, chart, editing and deleting rows |
| `MainViewModelTests` | 3 | assembling the screens, recording a pomodoro, closing the menu |

Every layer except markup is covered: the domain, statistics calculation, geometry,
formatting, the store and the view models.

## What is not covered

**Markup tests.** Bindings can be checked automatically, but for an application
this size that costs more than looking at it. So windows are checked by hand
(and with UI Automation scripts when you need to make sure nothing fell off).

**Dedicated `TimerViewModel` tests.** Its own logic is retelling the domain
as strings for the screen, and everything else is checked from `MainViewModelTests`:
a pomodoro started by a command and driven to the end by the ticker reaches the store.

**Sound and tray tests.** There is nothing to check there without hardware.

## Fakes

Everything lives in `tests/Pomidoro.Tests/Fakes/`.

### `FakeTime`

```csharp
internal sealed class FakeTime : TimeProvider
{
    public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;
    public override long TimestampFrequency => TimeSpan.TicksPerSecond;
    public override DateTimeOffset GetUtcNow() => _utcNow;
    public override long GetTimestamp() => _utcNow.UtcTicks;

    public void Advance(TimeSpan delta) => _utcNow = _utcNow.Add(delta);
}
```

Both the wall clock and the monotonic counter run off the same field, so `Advance`
moves everything at once. The time zone is UTC so that "now" in tests is predictable.

### `FakeTicker`

Remembers whether it is running and can `Fire()` — raise `Tick` by hand.

### `FakeSessionStore`

`ISessionStore` on top of three in-memory lists. It repeats the store rules
that are visible from outside: assigns `Id`, filters by logical day, drops
duplicate types case-insensitively.

For assertions it also records what was done to it:

```csharp
public List<string> UpdatedTypes { get; } = [];
public List<long> DeletedIds { get; } = [];
```

This is where, for example, the "showing a row writes nothing to the database" test
comes from: after three rows are created, `UpdatedTypes` must be empty.

### `TestSessions`

```csharp
public static WorkSession At(DateTime local, int minutes, string type = "") => new()
{
    EndUtc = local.ToUniversalTime(),
    DurationSec = minutes * 60,
    Type = type
};
```

Sessions are given in **local** time and converted to UTC. This matters: `WorkSession.EndLocal`
is computed in the machine's time zone, and if the time were given in UTC directly, the tests
would pass in Moscow and fail in London.

## How they are written

### Names

A test name is an English phrase joined with underscores that reads as a statement:

```
Paused_time_does_not_count
Same_type_written_in_different_case_stays_one_type
Garbage_rolls_back_to_the_previous_value
```

In a test run report such a list reads as a description of behavior. The analyzer for names
with underscores (CA1707) is turned off for the `tests/**` folder in `.editorconfig`.

### One test — one statement about behavior

```csharp
[Fact]
public void Finished_work_is_recorded_with_its_planned_length_and_rest_starts()
{
    _timer.Start(Work, Rest);

    // The tick came after zero, but exactly 25 minutes must go into the history.
    _time.Advance(Work + TimeSpan.FromMilliseconds(80));
    TimerStep step = _timer.Advance();

    Assert.Equal(TimerEvent.WorkFinished, step.Event);
    Assert.Equal(25 * 60, step.Completed.DurationSec);
    Assert.Equal(Rest, _timer.PhaseLength);
}
```

Several `Assert`s in one test are fine if they are about the same event.

### Tables instead of copy-paste

```csharp
[Theory]
[InlineData(0, "ни одной помидорки")]
[InlineData(1, "1 помидорка")]
[InlineData(3, "3 помидорки")]
[InlineData(5, "5 помидорок")]
[InlineData(11, "11 помидорок")]
[InlineData(21, "21 помидорка")]
[InlineData(114, "114 помидорок")]
public void Pomodoros_follow_russian_rules(int count, string expected)
```

### Store tests run against real SQLite

```csharp
_path = Path.Combine(Path.GetTempPath(), "pomidoro-tests", Guid.NewGuid().ToString("N"), "test.db");
_store = new SqliteSessionStore(_path);
```

Each test gets its own database in a temporary folder and deletes it in `Dispose`.
Faking the database here would be pointless: what is being checked is exactly the schema,
the date format, the migration and the SQL — things a fake would have to invent anew.

It was these tests that found the connection staying in the pool and holding the file:
deleting the folder failed with "the file is being used by another process". The cure is `Pooling=false`.

### Environment-independent checks

Where behavior depends on the time zone, what is checked is not a specific value
but an invariant:

```csharp
DateTime start = boundary.StartUtc(day).ToLocalTime();
DateTime end = boundary.EndUtc(day).ToLocalTime().AddSeconds(-1);

Assert.Equal(day, boundary.DayOf(start));
Assert.Equal(day, boundary.DayOf(end));
```

Independence of formatting from the locale is checked directly — by swapping the culture:

```csharp
CultureInfo.CurrentCulture = new CultureInfo("ru-RU");
Assert.Equal("18.6 часа", Format.Hours(66967));
```

## How to add a test

1. The file goes into `tests/Pomidoro.Tests/`, named `TypeNameTests.cs`.
2. The class is `public`, methods are marked `[Fact]` or `[Theory]`.
3. The application's internal types are visible thanks to `InternalsVisibleTo` in `Pomidoro.csproj` —
   nothing else needs to be referenced.
4. If you need time, a ticker or a store — take the fakes from `Fakes/`, not the real ones.
5. If you are checking something that used to be broken, say exactly what in a comment:
   that way the test won't be deleted a year later as "weird".

An example of a complete view model test:

```csharp
[Fact]
public void Click_leaves_only_the_type_that_was_clicked()
{
    _stats.ToggleType("Работа", shiftHeld: false);

    Assert.Equal([true, false, false], Shown());
    Assert.Equal("1.0 часа", _stats.HoursText);
    Assert.Single(_stats.List.Rows);
}
```

Everything a person should see is checked at once: the state of the legend chips, the total hours
and the number of rows in the list.
