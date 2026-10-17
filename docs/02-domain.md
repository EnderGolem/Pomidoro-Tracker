# Domain

The `Domain/` folder holds the rules of the subject area. No database, no WPF, no files, no time
out of thin air: everything comes in as parameters. That is why everything here is covered by tests
without a single real second of waiting.

## `WorkSession` — a history entry

```csharp
internal sealed record WorkSession
{
    public long Id { get; init; }
    public required DateTime EndUtc { get; init; }
    public required int DurationSec { get; init; }
    public string Type { get; init; } = string.Empty;

    public DateTime EndLocal => EndUtc.ToLocalTime();
    public TimeSpan Duration => TimeSpan.FromSeconds(DurationSec);
}
```

This is the **only** thing that goes into the history: breaks and pauses are not saved.
The moment of finishing is recorded, not the start — it is the one that answers the question
"which day does this count towards".

Three decisions worth understanding:

- **`record`, not `class`.** `with` is needed: when minutes are edited in the list, the view
  model replaces the entry in its cache with a copy holding the new value
  instead of mutating it. Here immutability is cheaper than carefulness.
- **`Id = 0` for an entry that has not been saved yet.** The database assigns the identifier.
- **`Type` is a string, not a reference to the catalog.** A type removed from the catalog
  must stay in the history. More in the [Work types](#work-types) section.

This type has no `LocalDate` column, although the database does: it is a reference field
that the store fills in. See [03-storage.md](03-storage.md#schema).

## `PomodoroTimer` — the state machine

The most important part of the domain. The timer cannot tick on its own, save or chime —
it only counts time and reports what is happening right now.

### States

| Phase | Meaning |
|---|---|
| `Idle` | the timer is idle, waiting for "Start" |
| `Work` | a work interval is running |
| `Rest` | rest is running |

Plus a "paused" flag, which applies to `Work` and `Rest`. There is no pause while idle.

### Transitions

| Before | Called | After | Side result |
|---|---|---|---|
| `Idle` | `Start(work, rest)` | `Work` | — |
| `Work`/`Rest` | `Start(...)` | unchanged | a repeated start is ignored |
| `Work`/`Rest` running | `Pause()` | same phase, paused | the accumulated time is frozen |
| paused | `Resume()` | same phase, running | — |
| `Work` | `Advance()` with `Remaining == 0` | `Rest` | `TimerStep(WorkFinished, entry)` |
| `Rest` | `Advance()` with `Remaining == 0` | `Idle` | `TimerStep(RestFinished, null)` |
| `Work` | `Stop()` | `Idle` | an entry, if ≥ 1 second was worked |
| `Rest` | `Stop()` | `Idle` | nothing is recorded |
| any | `Advance()` before the phase ends or while paused | unchanged | `TimerStep.Nothing` |

### How time is counted

```csharp
public TimeSpan Elapsed => _runningSince is long since
    ? _elapsedBeforePause + _time.GetElapsedTime(since)
    : _elapsedBeforePause;
```

Two fields: `_elapsedBeforePause` is how much has accumulated so far, `_runningSince` is the time stamp
of the start of the current stretch (or `null` while paused). `Pause` moves the current
stretch into the accumulated time and clears the stamp; `Resume` sets the stamp again.

Two consequences follow:

- **Time spent paused does not accumulate anywhere.** It is not "subtracted later"; it simply does not run.
- **The number of ticks has no effect.** If the interface freezes for half a second,
  the timer does not fall behind: it looks at time stamps, not at how many calls it got.

`GetElapsedTime` uses a monotonic counter (`GetTimestamp`), not the wall clock,
so changing the system time or an NTP correction does not break the timer.

### Why the planned duration is recorded

```csharp
// Use the planned duration, not the measured one: the tick may arrive tens of
// milliseconds after zero, and in the history it would look like 25:00.08.
WorkSession completed = NewSession(PhaseLength);
```

A tick arrives every 100 ms, so we always overshoot the "exactly zero" moment.
If the measured value were recorded, the database would pile up 1500.07 s instead of 1500.
On an early stop, however, the measured value is exactly what is written — the planned duration
makes no sense there.

The `MinRecordedSeconds = 1` threshold cuts off the "pressed Start and immediately Stop" case.

### Where time comes from

```csharp
public PomodoroTimer(TimeProvider time)
```

`TimeProvider` is the standard way in .NET 8+ to avoid depending on the clock directly.
The application passes `TimeProvider.System`; tests pass `FakeTime`, whose
time is moved by hand:

```csharp
_timer.Start(Work, Rest);
_time.Advance(TimeSpan.FromMinutes(10));
_timer.Pause();
_time.Advance(TimeSpan.FromHours(3));   // paused — does not count
Assert.Equal(TimeSpan.FromMinutes(10), _timer.Elapsed);
```

A full cycle with pauses is checked instantly. See [06-testing.md](06-testing.md).

### `TimerStep` and `TimerEvent`

`Advance()` returns neither `void` nor `bool`, but a description of what happened:

```csharp
internal readonly record struct TimerStep(TimerEvent Event, WorkSession? Completed)
{
    public static TimerStep Nothing => new(TimerEvent.Nothing, null);
}
```

`TimerEvent` is `Nothing`, `WorkFinished` or `RestFinished`. Returning a value instead of raising events
or using `out` parameters was a deliberate choice: the calling code reads top to bottom in one piece,
and a test can simply compare the result.

## `DayBoundary` — where the day ends

```csharp
internal readonly record struct DayBoundary
{
    public const int MinHour = 0;
    public const int MaxHour = 23;
    public const int DefaultHour = 0;
}
```

The hour is set in the settings and clamped to the allowed range right in the constructor:
`new DayBoundary(99).Hour == 23`. There is no need to validate the value outside.

| Method | What it does |
|---|---|
| `DayOf(DateTime local)` | which logical day a moment belongs to |
| `Today(TimeProvider time)` | which logical day it is now |
| `StartUtc(DateOnly day)` | the start of the day in UTC |
| `EndUtc(DateOnly day)` | the end of the day in UTC (which is also the start of the next one) |

All of the arithmetic boils down to one line:

```csharp
public DateOnly DayOf(DateTime local) => DateOnly.FromDateTime(local.AddHours(-Hour));
```

Shift the moment back by the boundary and take the calendar date. With a boundary of `4`,
`October 17, 03:59` becomes `October 16, 23:59` — and counts towards
the sixteenth. With a boundary of `0` nothing changes.

The store needs `StartUtc`/`EndUtc` to turn a "day" into a pair of stamps for `WHERE`:

```csharp
public DateTime StartUtc(DateOnly day) =>
    DateTime.SpecifyKind(day.ToDateTime(TimeOnly.MinValue).AddHours(Hour), DateTimeKind.Unspecified)
        .ToUniversalTime();
```

The date plus the boundary hour is treated as local time and converted to UTC.

> **Edge case.** If the boundary falls on an hour that is skipped when switching to daylight
> saving time, `ToUniversalTime` picks the nearest valid moment. Russia does not
> change its clocks, so there is no point in handling this case in the application.

The tests are deliberately written not to depend on the machine's time zone:
they do not check a specific UTC moment, but that the start and end of a day fall
back into the same day.

## `DayRange` — a range of days

```csharp
internal readonly record struct DayRange(DateOnly From, DateOnly To)
```

It exists so that a `(from, to)` pair is not dragged around the code and the two are not mixed up.
It does little: `Days` (how many days, both ends included), `Contains`, `EnumerateDays`,
`IsSingleDay` and `SingleDay(day)` for "today".

`EnumerateDays` returns **all** days of the range, including empty ones — the chart
relies on this: a day without work must still take its place on the axis.

## `StatsPeriods` — statistics periods

The `StatsPeriod` enum: `CurrentWeek`, `PreviousWeek`, `CurrentMonth`, `PreviousMonth`.

```csharp
public static DayRange Resolve(StatsPeriod period, DateOnly today) => period switch
{
    StatsPeriod.CurrentWeek  => new DayRange(WeekStart(today), today),
    StatsPeriod.PreviousWeek => new DayRange(WeekStart(today).AddDays(-7), WeekStart(today).AddDays(-1)),
    StatsPeriod.CurrentMonth => new DayRange(MonthStart(today), today),
    StatsPeriod.PreviousMonth=> new DayRange(MonthStart(today).AddMonths(-1), MonthStart(today).AddDays(-1)),
    _ => DayRange.SingleDay(today)
};
```

Current periods end **today**, not on Sunday and not on the 31st: there is no point in showing
an empty tail of the future. Previous periods are taken whole.

A week starts on Monday:

```csharp
private static DateOnly WeekStart(DateOnly day) => day.AddDays(-(((int)day.DayOfWeek + 6) % 7));
```

`DayOfWeek` in .NET counts Sunday as zero, so `+6 % 7` shifts the origin.
For Sunday this gives 6 — and the week still starts on the preceding Monday.

`Title(period)` gives the label for the drop-down, and `All` gives all values in order.
The order in `All` determines the order of items on screen.

## Work types

`WorkTypes` is a small class that all handling of types rests on.

```csharp
public const string NoTypeLabel = "Без типа";    // legend label
public const string NoTypeOption = "—";          // drop-down item in a row
public static readonly StringComparer Comparer = StringComparer.OrdinalIgnoreCase;
```

### An empty type and "no type" are different things

The grouping key for an entry without a type is **an empty string**. The "No type" label
is substituted only when displaying:

```csharp
public static string Display(string canonicalType) =>
    canonicalType.Length == 0 ? NoTypeLabel : canonicalType;
```

So a type the user might name "No type" does not merge with the genuinely
empty ones. Just in case, such a name (as well as the dash) is simply not allowed into the catalog:
`IsReservedName`.

### Normalizing to the catalog spelling

```csharp
public static string Canonical(string? storedType, IReadOnlyList<string> catalog)
```

It takes what is stored in the entry and returns the spelling from the catalog if there is
a case-insensitive match. If there is no match, it returns the value as is, with whitespace trimmed.
An empty string and whitespace turn into the empty key.

Why this is needed:

- the catalog is **case-insensitive** (you cannot add both «созвон» and «СОЗВОН»);
- entries, however, may hold anything — for example, after a type was removed
  from the catalog and added back with a different capitalization, or after an edit through the SQL console;
- if grouping were done by the raw string, «Работа» and «работа» would produce **two legend chips,
  two colors on the chart and two separate filters**, even though both rows
  look the same in the list.

Normalization is done **only for display**. The application does not rewrite the history itself:
what was written is what stays. You can check this through the SQL console — both rows are visible there.

`Comparer` and `NewSet()` are used wherever types are compared or collected
into a set: the set of hidden types, color dictionaries, groupings.

### Why case is compared in C# and not in SQL

SQLite's built-in `COLLATE NOCASE` folds case **only for Latin `A-Z`**.
It is useless for Cyrillic: it treats «созвон» and «СОЗВОН» as different words.
That is why the duplicate check lives in `SqliteSessionStore.AddTypeToCatalog`,
and the schema keeps a plain `UNIQUE`.
