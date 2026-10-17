# Architecture

## The main idea

The application is split into layers, and dependencies between them point **only one way**:

```
Views  ──►  ViewModels  ──►  Stats  ──►  Domain
                │                          ▲
                └──────►  Storage  ────────┘
                             │
                        Formatting
                       Localization
```

It reads like this:

- **Domain** knows nothing: not the database, not the screens, not WPF.
- **Stats** knows only about Domain.
- **Storage** knows about Domain (it needs to know what exactly to save) and nothing else.
- **ViewModels** know about all of the above, but not about the markup.
- **Views** know only about ViewModels, and only through bindings.

The point of the rule is simple: the lower the layer, the easier it is to test. `Domain` and `Stats` are
ordinary classes with ordinary methods; they need no window, no file and no dispatcher,
so they are fully covered by tests. The higher the layer, the less logic it has:
by the time you reach the markup, not a single computation is left.

The rule can be checked by eye: `Domain/` and `Stats/` contain no `using System.Windows`
at all, except `Stats/TypePalette.cs`, which holds the brushes for the chart.

## What lives where

### `Domain/` — the rules of the subject area

Timer, days, periods, work types, history entries. Details in [02-domain.md](02-domain.md).

There is not a single `async`, not a single call to the outside world and not a single piece of mutable
global state here. Time comes from outside through `TimeProvider`.

### `Stats/` — computations for the statistics screen

`StatsCalculator` turns a list of entries into totals and a breakdown by day and by type.
`ChartLayout` turns seconds and available pixels into heights and widths.
Details in [04-statistics.md](04-statistics.md).

### `Storage/` — SQLite

`ISessionStore` is the contract for "where to put history and settings". `SqliteSessionStore` is
the only implementation. `AppSettings` is a convenient wrapper over key-value pairs.
`ISqlConsole` is a separate contract for the developer console.
Details in [03-storage.md](03-storage.md).

### `Formatting/` — text

Everything that turns a number or a date into an on-screen string: `Format`, `Plural`,
`SqlResultText`. There is not a single `ToString` without an explicit culture in the project —
otherwise on a machine with a different locale both the decimal separator and the calendar would go off.

### `Localization/` — interface text

All the Russian and English text, the current language and the `{loc:Text Key}` markup extension.
`Strings` is declared as an abstract class, so a missing translation does not compile.
Details in [10-localization.md](10-localization.md).

### `Mvvm/` — a minimal foundation

Four small files instead of a library:

| File | Why |
|---|---|
| `ObservableObject` | base class with `PropertyChanged` and a `Set` helper |
| `RelayCommand` | a parameterless command with an optional can-execute check |
| `RelayCommand<T>` | a command with a parameter (a list row, a type in the legend) |
| `ITicker` / `DispatcherTicker` | tick source for the timer |

Home-grown rather than a ready-made package for one reason: all of it fits into a hundred lines,
can be read in full in five minutes and needs no code generators. More in
the [decision log](08-decisions.md).

### `ViewModels/` — screen state

Each screen is a separate class with properties the markup binds to
and commands the buttons invoke. Details in [05-ui.md](05-ui.md).

### `Views/` and `Themes/` — markup

Windows, screens and styles. Code-behind is empty, except in three places where it cannot be avoided
(see [05-ui.md](05-ui.md#three-exceptions)).

## How the application is wired up at startup

The only place where the layers meet each other is `App.xaml.cs`.
This is the composition root: no class creates its own dependencies,
everything comes in through the constructor.

`App.OnStartup` step by step:

1. **Subscribing to unhandled exceptions.** Any error that reaches the dispatcher
   shows a message and closes the application — instead of the system "program is not responding" window.
2. **Opening the database.** `TryOpenStore()` creates a `SqliteSessionStore` at
   `AppContext.BaseDirectory + pomodoro.db`. If the file could not be opened (no permissions, a read-only
   disk, a corrupted file), a message with the reason is shown and
   `Shutdown(1)` is called. The application has no reason to run without a database.
3. **Settings.** `AppSettings` reads three values from the `Settings` table.
4. **View models.** `MainViewModel` is created, and it creates the other four.
   `TimeProvider.System` and a `DispatcherTicker` with a 100 ms interval are passed in here as well.
5. **Sound.** `Timer.PhaseFinished` is wired to `Chime.Play()` — here rather than inside
   the model, so that the models stay free of side effects and can be tested.
6. **Tray.** `TrayIcon` with "Open" and "Exit" items.
7. **Window.** `MainWindow` with `DataContext = MainViewModel`; the icon is drawn in memory.
8. **Intercepting the close button.** `Closing` is handled right here: the window hides into the tray.

```csharp
_viewModel = new MainViewModel(store, settings, TimeProvider.System, new DispatcherTicker(TickInterval));
_viewModel.Timer.PhaseFinished += (_, _) => Chime.Play();
_viewModel.SqlConsoleRequested += (_, _) => ShowSqlConsole(store);
```

There is no dependency injection container and none is needed: the graph is small, and seeing it whole
in one method is more useful than hunting through attributes.

## Window lifecycle

`ShutdownMode` is set to `OnExplicitShutdown`, meaning closing windows does not by itself
quit the application. This matters because the window hides into the tray rather than closing.

| Event | What happens |
|---|---|
| Close button | `OnMainWindowClosing` cancels the close, hides the window and removes it from the taskbar |
| Click on the tray icon | `RestoreFromTray`: the window is shown, restored, activated and **reloads the open screen** |
| "Exit" in the tray menu | `ExitApplication`: sets `_exiting = true` and calls `Shutdown()` |
| Windows session ending | `OnSessionEnding` sets `_exiting = true`, and everything else proceeds as usual |
| Unhandled exception | a message with the error type and text, then `ExitApplication` |

The `_exiting` flag is the only thing that distinguishes "hide" from "close". Without handling
`OnSessionEnding`, the window would refuse to close when the session ends.

Nothing is lost on a crash: settings are written to the database immediately on change,
and every pomodoro at the moment it finishes.

## How data reaches the screen

### A pomodoro finished

```
DispatcherTicker (every 100 ms)
    └─► TimerViewModel.Advance()
          └─► PomodoroTimer.Advance()  →  TimerStep { Event, Completed }
                ├─ Completed is set  →  TimerViewModel.WorkCompleted
                │                        └─► MainViewModel.OnWorkCompleted
                │                              ├─ store.Add(session)
                │                              └─ ReloadVisibleScreen()
                └─ Event ≠ Nothing  →  TimerViewModel.PhaseFinished  →  Chime.Play()
```

Note that the timer knows about neither the database nor the sound. It returns a description
of what happened, and the layers above decide what to do about it.

### Statistics opened

```
MainViewModel.IsStatsVisible = true
    └─► StatsViewModel.Reload()
          ├─ store.GetTypeCatalog()
          ├─ StatsPeriods.Resolve(period, today)    →  DayRange
          ├─ store.GetRange(range, boundary)        →  list of entries
          └─ Refresh(rebuildList: true)
                ├─ StatsCalculator.Build(...)       →  StatsSummary
                ├─ BuildLegend(summary)             →  ObservableCollection<LegendItemViewModel>
                ├─ BuildChart()                     →  Columns + GridLines
                └─ List.Load(visible entries)       →  list rows
```

The markup binds to these collections and properties — and does nothing else.

### Minutes edited in a row

```
TextBox loses focus
    └─► SessionRowViewModel.Minutes = "40"
          ├─ parsing and range check (0…1440)
          ├─ store.UpdateDuration(id, 2400)
          └─ Changed
                └─► StatsViewModel.OnRowChanged
                      ├─ replaces the entry in its cache (record with { ... })
                      └─ Refresh(rebuildList: false)
```

The list is deliberately **not** rebuilt: otherwise the row that was just edited
would slide out from under the cursor. It is rebuilt only when the set of rows
itself changes — on reload, on deleting an entry or on toggling a legend filter.

### A query ran in the console

```
SqlWindow (button, F5 or Ctrl+Enter)
    └─► SqlConsoleViewModel.Run(selection or the whole text)
          ├─ ISqlConsole.Execute(sql)       →  SqlQueryResult
          ├─ Output = SqlResultText.Render(result)
          ├─ Status = SqlResultText.Status(result)
          └─ Executed
                └─► App.RefreshAfterSql
                      └─ try { MainViewModel.ReloadAfterSql() }
                         catch { console.ReportRefreshFailure(ex) }
```

The query may have dropped a table the screen relies on, so the refresh is wrapped
in a `try`. The error is appended to the console's own output — the application stays alive.

## What counts as an architecture violation

Signs that something has gone wrong:

- `using System.Windows` showed up in `Domain/`;
- `using Microsoft.Data.Sqlite` showed up anywhere outside `Storage/`;
- a computation crept into the markup (a converter with complex logic, arithmetic in a binding);
- a view model started creating a window directly;
- `Views/*.xaml.cs` gained code that is about neither input nor sizes.

Any of these is a reason to move the code one layer down.
