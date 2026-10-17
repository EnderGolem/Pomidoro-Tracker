# Documentation

This is a detailed walkthrough of how the pomodoro tracker is built: what it is made of, why
it is made this way and where to find things. The root [README](../README.md) answers the question
"what is this and how do I run it"; this one answers "how does it work inside".

## Where to start

If you are opening the project for the first time, read in order — each part builds
on the previous one:

1. **[Architecture](01-architecture.md)** — layers, dependency rules, how the application
   is wired up at startup and how data reaches the screen. The most important part.
2. **[Domain](02-domain.md)** — timer, days, periods, work types. Pure logic
   with neither a database nor an interface.
3. **[Storage](03-storage.md)** — SQLite: schema, queries, migrations, settings, console.
4. **[Statistics](04-statistics.md)** — how totals, the chart and the median are derived
   from a list of entries.
5. **[Interface](05-ui.md)** — MVVM: view models, screens, bindings, styles.
6. **[Tests](06-testing.md)** — what is tested, how the fakes work and how to add your own.
7. **[Build and release](07-build.md)** — configurations, analyzers, publishing, CI.
8. **[Decision log](08-decisions.md)** — why things are done this way and not another, and what was
   rejected. Read it when you wonder "why is this workaround here".
9. **[Recipes](09-recipes.md)** — step by step: add a setting, a period, a column, a field.
10. **[Languages](10-localization.md)** — Russian and English: where the strings are, how the language is chosen.

To understand the structure quickly, four code files are enough:
`App.xaml.cs` → `Domain/PomodoroTimer.cs` → `ViewModels/MainViewModel.cs` →
`Stats/StatsCalculator.cs`.

## Project map

```
Pomidoro/
├─ App.xaml, App.xaml.cs      entry point and application wiring
├─ Domain/                    timer, days, periods, types — no WPF and no database
├─ Stats/                     statistics and chart geometry — pure functions
├─ Storage/                   SQLite, settings, developer console
├─ Formatting/                numbers and dates → on-screen text
├─ Localization/              Russian and English interface text
├─ Mvvm/                      notifications, commands, tick source
├─ ViewModels/                screen state and button commands
├─ Views/                     markup of windows and screens
├─ Themes/                    colors and styles
├─ Audio/                     end-of-phase chime
├─ Tray/                      notification area icon
├─ tests/Pomidoro.Tests/      162 tests
└─ docs/                      this folder
```

## Code conventions

- **Everything is internal.** Types are declared `internal`: this is an application, not a library.
  Tests see them through `InternalsVisibleTo` in `Pomidoro.csproj`.
- **Explicit types instead of `var`.** Code is read more often than it is written.
- **One type per file**, and the file name matches the type name.
- **No XML documentation.** Explanations live here and in short comments
  where behaviour is not obvious; names should speak for themselves.
- **A comment answers "why", not "what".** If a comment retells
  the code, it is redundant.
- **Member order in a class:** constants, fields, constructor, events, properties,
  public methods, private methods.
- **Encoding:** UTF-8 without BOM, CRLF line endings, four-space indentation.
- **Compiler and analyzer warnings are build errors.**
