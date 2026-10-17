# Pomodoro tracker

A desktop Pomodoro-technique timer for Windows: work → chime → rest → chime.
Every interval you work goes into a local SQLite database and then shows up
in the statistics — by day, by work type and as a single number for the period.

WPF, .NET 9, one window, no network and no accounts. The database sits next to the `.exe`.

---

## Features

- **Timer.** Work and rest durations are set in minutes and remembered between runs.
  Paused time does not count. "Stop" during work still saves the time already worked.
- **Today.** Total hours, number of pomodoros and the list of intervals. Minutes and type
  can be edited right in the row.
- **Statistics.** Current or previous week or month: a per-day chart broken down
  by type, filter buttons under it, the daily median and the full list of entries.
- **Day boundary.** Night work after midnight can count towards the previous day —
  the switch-over hour is configurable.
- **Tray.** The close button hides the window, the timer keeps running, and the chime plays regardless.
- **SQL console** for your own queries against the database.
- **Two languages.** Russian and English — as two separate builds, with no switch
  in the interface. They share the same database.

---

## Building and running

```
dotnet build Pomidoro.sln -c Debug
bin\Debug\net9.0-windows\Pomidoro.exe
```

Configurations: `Debug`, `Release`.

The .NET analyzers are enabled and warnings are treated as errors
(`TreatWarningsAsErrors`), so "it builds" means "it builds cleanly".
If that gets in the way, it is switched off with one line in `Pomidoro.csproj`.

### Tests

```
dotnet test tests\Pomidoro.Tests\Pomidoro.Tests.csproj -c Debug
```

There are 162 of them right now. They cover the computational and domain parts: the timer
state machine, the day boundary, period resolution, all of the statistics arithmetic, chart
geometry, formatting, type normalization, the whole store (against real SQLite in a temporary
file) and view model behaviour — including the legend rules and editing list rows.

Markup and the windows themselves are not covered by tests: they are checked by eye.

### Single-file publishing

```
dotnet publish -p:PublishProfile=SingleFile-Ru
dotnet publish -p:PublishProfile=SingleFile-En
dotnet publish -p:PublishProfile=SingleFile-Framework
```

| Profile | Language | Size | Needs .NET on the machine |
|---|---|---|---|
| `SingleFile-Ru` | Russian | ~72 MB | no |
| `SingleFile-En` | English | ~72 MB | no |
| `SingleFile-Framework` | Russian | ~2.4 MB | yes, .NET 9 Desktop Runtime |

The result is a single `Pomidoro.exe` in `publish\ru\`, `publish\en\` or `publish\framework\`.
The database is created next to it, not in the temporary extraction folder. The self-contained
build cannot be squeezed further: `PublishTrimmed` is not supported with WPF, and compression is already on.

---

## How the code is organized

> This is an overview. A detailed walkthrough of every layer, the decision log and recipes
> for typical changes live in **[docs/](docs/README.md)**.

The application is split into layers: **domain → store → view models → markup**.
There is one rule — the arrows only point one way. The domain knows nothing about the database,
the database knows nothing about screens, and screens do not know where the data came from.

```
App.xaml.cs            ← everything is wired together here: database, settings, models, windows, tray
  │
  ├─ Storage/          SQLite: schema, writing, reading, settings, console
  ├─ Domain/           timer, days, periods, types — pure logic without WPF
  ├─ Stats/            statistics and chart geometry — also pure functions
  ├─ Formatting/       everything that turns numbers and dates into on-screen text
  ├─ Localization/     Russian and English interface text
  ├─ ViewModels/       screen state and button commands
  ├─ Views/            markup: what is drawn where
  └─ Themes/           colors and styles, not a single color in the markup itself
```

### Where things live

| File | What is inside |
|---|---|
| `App.xaml.cs` | entry point and application wiring; tray, windows, crash exit |
| `Domain/PomodoroTimer.cs` | state machine: work → rest → idle, pauses, early stop |
| `Domain/DayBoundary.cs` | where one day ends and the next begins |
| `Domain/DayRange.cs` | a range of days with enumeration and a containment check |
| `Domain/StatsPeriods.cs` | "current week", "previous month" → concrete dates |
| `Domain/WorkTypes.cs` | bringing a type to its spelling in the catalog; reserved names |
| `Domain/WorkSession.cs` | a history entry: when it finished, how long it lasted, what type |
| `Stats/StatsCalculator.cs` | totals, breakdown by day and type, median |
| `Stats/ChartLayout.cs` | bar width, grid step, segment height — in pixels |
| `Stats/TypePalette.cs` | which type gets which color |
| `Storage/SqliteSessionStore.cs` | the only store implementation |
| `Storage/SqliteSchema.cs` | schema and migration of old databases |
| `Storage/AppSettings.cs` | settings: read at startup, written immediately on change |
| `Formatting/Format.cs` | numbers, minutes, dates, ranges — always in the invariant culture |
| `Localization/Strings.cs` | all interface text; the subclasses are Russian and English |
| `Mvvm/` | base class with notifications, commands, tick source |
| `ViewModels/MainViewModel.cs` | which screen is shown and who reloads the database after edits |
| `ViewModels/StatsViewModel.cs` | period, chart, legend, list for the period |
| `ViewModels/SessionRowViewModel.cs` | one list row: editing minutes and type |
| `Views/` | windows and screens; code-behind holds only keyboard input and panel size |

### How data reaches the screen

A finished pomodoro: `PomodoroTimer` hands out a `WorkSession` → `TimerViewModel` reports
it upwards → `MainViewModel` puts it into the database and asks the open screen to reload itself.

The statistics screen: `StatsViewModel.Reload()` fetches the sessions for the period from the database,
`StatsCalculator` computes totals, days and types from them, `ChartLayout` turns that
into pixels, and the markup displays the resulting lists. There is not a single computation
in the markup itself — which means all of it can be tested.

### MVVM and three exceptions

Behaviour lives in the view models; the markup only binds to their properties
and commands. Code-behind is empty — except in three places where it cannot be avoided:

- `SessionListView`, `SettingsView` — input filtering (only digits are allowed in the minutes field)
  and Enter in a cell. This is keyboard handling, not logic.
- `StatsView` — the size of the chart panel. `ActualWidth` is read-only, so there is
  nothing to bind it to the model with.
- `SqlWindow` — the selected fragment of the query. `TextBox.SelectedText` is not
  a dependency property.

---

## How the timer works

- **Start** — starts work. When it ends, a chime plays and rest begins immediately;
  after rest comes a second chime, and the timer returns to idle.
- **Pause** — freezes the countdown. Time spent paused does **not** count and is not saved anywhere.
- **Stop** — resets the timer. If work was cut short, the time worked still goes
  into the history (anything longer than a second).

The countdown is driven by monotonic time stamps, not by counting ticks: when the
interface stutters, the timer does not drift. The history records the planned duration, not the measured one,
otherwise the list would show "25 minutes and 80 milliseconds".

Time reaches the timer through `TimeProvider` — so in tests a full cycle with pauses
is checked instantly, without a single real second of waiting.

---

## Day boundary

By default the day ends at midnight. If you work at night, it is handier to move the boundary:
with a value of `4`, everything closed before four in the morning goes into the previous day.

That is why queries go **by `EndUtc`** rather than by calendar date: the boundary can be moved
at any moment, and the whole history is immediately recomputed under the new rule.

---

## Work types

A type is a "what was I working on" label. It is chosen in a list row from the catalog,
which is configured under the gear.

In an entry the type is stored as **a plain string**, not a reference: removing a type from the catalog is safe,
it does not disappear from the history — it simply stops being offered for new entries.
A row tagged with a retired type still shows it in its drop-down,
otherwise the choice could not be restored.

**Case.** The catalog is case-insensitive: «созвон» and «СОЗВОН» are one type. The check
is done in C#, not with a `COLLATE NOCASE` constraint: SQLite's built-in `NOCASE` folds
only Latin `A-Z` and treats Cyrillic in different case as different words.

This gives a rule for all of the statistics: the type from an entry is brought to its spelling in
the catalog (`WorkTypes.Canonical`), and grouping is done by that. Otherwise «Работа» and
«работа» would split into two legend chips, two colors on the chart and two separate
filters — even though both rows would look the same in the list. The application does
not rewrite the history itself: the database keeps exactly what was put there.

---

## Statistics

**Period** — the current or previous week, the current or previous month. A week starts
on Monday.

**Chart.** A bar for every day of the period, including empty ones. Inside a bar there are segments
by type, the longest at the bottom. The axis ceiling is rounded up to a round value so that
the top grid line is labelled with a whole number of hours. Weekends are dimmed.

**The legend** is a filter:

- clicking a type keeps **only that one**;
- clicking the only enabled type brings back **all of them**;
- **Shift+click** adds or removes one type without touching the others.

Disabled types live only in memory: this is a view of the data, not a setting, and after
a restart everything is shown again.

**The median** is computed over the days **on which there was work**. In a month with six working days
out of thirty, a median over all days would always be zero and would say nothing.

---

## Window and notification area

The close button does not quit the application; it moves the window into the notification area — on Windows 11
the icon is usually hidden under the chevron. The timer keeps running, and the chime plays even
if the window is not on screen.

To bring the window back, click the icon. The right button opens a menu with "Open" and "Exit";
only "Exit" and the end of the Windows session actually quit the application.

When the window comes back from the tray, the open screen is reloaded: the window may have sat there until the next
day, and then "today" is a different day.

Settings are saved immediately on change, so a crash loses nothing.

---

## Where the data lives

The `pomodoro.db` file next to `Pomidoro.exe`. The application folder can be copied as a whole —
the history moves with it. Backup = a copy of the file.

```sql
CREATE TABLE WorkSessions (
    Id          INTEGER PRIMARY KEY AUTOINCREMENT,
    EndUtc      TEXT    NOT NULL,            -- end moment in UTC, ISO 8601
    LocalDate   TEXT    NOT NULL,            -- local end date, 'YYYY-MM-DD'
    DurationSec INTEGER NOT NULL,            -- net work time in seconds, without pauses
    Type        TEXT    NOT NULL DEFAULT ''  -- user label, may be empty
);
CREATE INDEX IX_WorkSessions_EndUtc ON WorkSessions (EndUtc);
CREATE INDEX IX_WorkSessions_LocalDate ON WorkSessions (LocalDate);

CREATE TABLE Settings (Key TEXT PRIMARY KEY, Value TEXT NOT NULL);

-- type catalog: only suggestions for the drop-down, does not affect history
CREATE TABLE TypeCatalog (Id INTEGER PRIMARY KEY AUTOINCREMENT, Name TEXT NOT NULL UNIQUE);
```

`EndUtc` is stored in UTC so that the history does not shift when the time zone changes. `LocalDate`
is a reference column for manual queries: the application selects nothing by it, because
the day boundary is configurable and may have moved after the entry was written.

The `Type` column appeared later than the rest, so when an old database is opened it is added
via `ALTER TABLE` — existing entries get an empty string, nothing is lost.

**Only work** is saved. Breaks and pauses are not written to the history.

---

## SQL console

Gear → "Developer SQL". The query runs **on the application's live connection,
without any checks**: `DROP`, `DELETE` and `UPDATE` run as written, and the history
cannot be brought back. This is a tool for yourself, not a user-facing feature.

`F5` or `Ctrl+Enter` runs the query; if there is a selection, only the selection runs.
Several statements separated by `;` run one after another. Output is cut off at 1000 rows.

After every query the open screen reloads the database. If the query destroyed something
the screen relies on, the error is shown in the console itself — the application does not crash because of it.

### Useful queries

```sql
-- hours per year
SELECT ROUND(SUM(DurationSec) / 3600.0, 1) AS hours
FROM WorkSessions
WHERE LocalDate LIKE '2026%';

-- by month
SELECT substr(LocalDate, 1, 7)              AS month,
       COUNT(*)                             AS pomodoros,
       ROUND(SUM(DurationSec) / 3600.0, 1)  AS hours
FROM WorkSessions
GROUP BY month
ORDER BY month;

-- where the time goes
SELECT CASE WHEN Type = '' THEN '(no type)' ELSE Type END AS type,
       COUNT(*)                            AS pomodoros,
       ROUND(SUM(DurationSec) / 3600.0, 1) AS hours
FROM WorkSessions
GROUP BY lower(Type)
ORDER BY hours DESC;

-- by day of week: when do I actually work
SELECT CASE CAST(strftime('%w', LocalDate) AS INTEGER)
           WHEN 0 THEN 'Sun' WHEN 1 THEN 'Mon' WHEN 2 THEN 'Tue' WHEN 3 THEN 'Wed'
           WHEN 4 THEN 'Thu' WHEN 5 THEN 'Fri' ELSE 'Sat' END AS day,
       ROUND(SUM(DurationSec) / 3600.0, 1) AS hours
FROM WorkSessions
GROUP BY strftime('%w', LocalDate)
ORDER BY strftime('%w', LocalDate);

-- at what hours of the day pomodoros finish
SELECT strftime('%H', datetime(EndUtc, 'localtime')) AS hour, COUNT(*) AS n
FROM WorkSessions
GROUP BY hour
ORDER BY hour;
```

---

## Decisions that look odd from the outside

- **WinForms in a WPF project.** It is referenced only for `NotifyIcon`: WPF has no tray icon
  of its own. Its implicit `using`s are disabled in the `.csproj`, otherwise `Button`, `TextBox` and
  `MessageBox` from WinForms clash with the WPF types of the same name throughout the code.
- **The icon and the sound are drawn in memory.** Neither an `.ico` nor a `.wav` needs to ship with the application,
  and the user's system sounds play no part.
- **`Pooling=false` in the connection string.** There is one connection, and it lives for the whole run,
  so the pool has nothing to gain. With the pool, however, `Dispose` does not release the database file, and it can be
  neither deleted nor copied right after closing.
- **The "No type" name is forbidden in the catalog.** An empty type and a type with that name are
  different things, and there is no need to confuse them in the legend.
- **Deleting an entry asks for no confirmation.** The trash icon appears only when hovering
  over a row, and this is a deliberate trade-off: a dialog on every edit in the list is more annoying.

## What is not here

Sync, accounts, notifications, export, a choice of dark and light themes,
multiple languages. This is a personal tool for a single machine.
