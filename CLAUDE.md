# Pomidoro

A desktop pomodoro tracker for Windows: WPF, .NET 9, SQLite next to the `.exe`.
One window, two screens (timer and statistics), a tray icon.

## Documentation

**A detailed walkthrough of how it is built is in [docs/](docs/README.md).** It also explains
why the code looks the way it does.

| Document | About |
|---|---|
| [docs/01-architecture.md](docs/01-architecture.md) | layers, dependencies, application wiring, data flow |
| [docs/02-domain.md](docs/02-domain.md) | timer, day boundary, periods, work types |
| [docs/03-storage.md](docs/03-storage.md) | SQLite: schema, queries, migrations, settings, console |
| [docs/04-statistics.md](docs/04-statistics.md) | statistics computation and chart geometry |
| [docs/05-ui.md](docs/05-ui.md) | MVVM, view models, screens, styles |
| [docs/06-testing.md](docs/06-testing.md) | what is covered by tests and how to write new ones |
| [docs/07-build.md](docs/07-build.md) | configurations, analyzers, publishing, CI |
| [docs/08-decisions.md](docs/08-decisions.md) | decision log: why this way and not another |
| [docs/09-recipes.md](docs/09-recipes.md) | step by step: add a setting, a period, a column |
| [docs/10-localization.md](docs/10-localization.md) | Russian and English: where the strings are, how the language is chosen at build time |

Four files are enough to understand the structure quickly:
`App.xaml.cs` → `Domain/PomodoroTimer.cs` → `ViewModels/MainViewModel.cs` →
`Stats/StatsCalculator.cs`.

## Build and tests

```
dotnet build Pomidoro.sln -c Debug
dotnet test tests\Pomidoro.Tests\Pomidoro.Tests.csproj -c Debug
bin\Debug\net9.0-windows\Pomidoro.exe
```

Build in `Debug` by default. Compiler and analyzer warnings
are treated as errors — "it builds" means "it builds cleanly".

## Editing rules

- **Layers are one-way:** `Views → ViewModels → Stats → Domain`, `ViewModels → Storage`.
  `Localization/` and `Formatting/` are leaves, referenced from above.
  `using System.Windows` must not appear in `Domain/` or `Stats/`
  (the exception is `Stats/TypePalette.cs` with its brushes). `Microsoft.Data.Sqlite` lives
  only in `Storage/`.
- **Logic does not live in markup.** If `Views/*.xaml.cs` gains code that is about neither keyboard
  input nor sizes, it is in the wrong place.
- **New behaviour is covered by a test** in `tests/Pomidoro.Tests/` unless it is
  about markup. There are fakes for time, the ticker and the store in `Fakes/`.
- **Types are `internal`**, one type per file, and the file name matches the type name.
- **Member order:** constants, fields, constructor, events, properties,
  public methods, private methods.
- **Explicit types instead of `var`.**
- **No XML documentation.** A comment answers "why", not "what";
  explanations go in `docs/`.
- **Numbers and dates are formatted only through `Formatting/Format`**, always
  in the invariant culture. The words around a number come from `Loc.S`.
- **No text in code or markup.** Every label is declared in `Localization/Strings.cs`
  and translated in both subclasses; in markup use `{loc:Text Key}`, in code `Loc.S.Key`.
- **The language is baked into the build** (`AppLanguage=en` gives the English one). There is no switching
  at runtime: the Russian and English versions are released as separate files.
- **Encoding:** UTF-8 without BOM, CRLF line endings, four-space indentation.

## What is easy to break unknowingly

- **Types are compared case-insensitively.** Grouping goes through
  `WorkTypes.Canonical(...)`, and sets of types are created with `WorkTypes.NewSet()`.
  Comparing raw strings brings back the bug where «Работа» and «работа» split
  into two legend chips.
- **Queries go by `EndUtc`, not by `LocalDate`.** The day boundary is configurable,
  and `LocalDate` knows nothing about it.
- **In row view model constructors, fields are assigned directly, bypassing the properties** —
  otherwise rendering the list writes to the database.
- **A dictionary in `Themes/` that uses colors must merge `Colors.xaml` itself**,
  otherwise `StaticResource` will not find the token.
- **WinForms implicit `using`s are removed in the `.csproj`** on purpose: otherwise `Button`,
  `TextBox` and `MessageBox` clash with the WPF types of the same name.
- **`.ps1` scripts for checking the UI must be saved with a BOM** — Windows
  PowerShell 5.1 reads UTF-8 without BOM as ANSI and breaks on Cyrillic.
- **The reserved "no type" name is forbidden in both languages at once** (`WorkTypes.ReservedNames`).
  The check must not depend on the current language.
- **A list bound to `ItemsSource` must not be rebuilt on the fly.**
  `ComboBox` will not find the selected item in the new list and will set it to `null`.
- **A binding with `UpdateSourceTrigger=LostFocus` does not push its value on `Keyboard.ClearFocus()`** —
  that clears keyboard focus, not logical focus. Confirming on Enter requires
  an explicit `UpdateSource()`, as in `SessionListView.xaml.cs`.
- **A property from a publish profile is not visible to a conditional `PropertyGroup`** in the `.csproj`:
  the profile is imported later. A target is needed — see `AddLanguageConstant`.
- **Do not write expected text as a literal in tests.** The same test suite runs
  for both language builds, so the expected value is taken from `Loc.S`.
