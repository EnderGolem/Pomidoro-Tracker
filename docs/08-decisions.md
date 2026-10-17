# Decision log

This records why things are done the way they are. Read it when the code looks odd
and you feel like "fixing" it — it may have been tried already.

Format: decision → why → what was considered instead.

---

## Storage and data

### The database sits next to the `.exe`, not in `%APPDATA%`

The application folder can be copied as a whole, and the history moves along with it;
a backup is a copy of a single file.

*Instead:* `%APPDATA%\Pomidoro\` — "more correct" by the guidelines, but then copying
the folder would stop working, and the application is meant to be carried around
on a flash drive.

*Cost:* if the `.exe` is put into `Program Files`, the database ends up in a directory without write
access. In that case the application shows a clear error and closes.

### Time is stored as text in the `"O"` format

It sorts lexicographically (so `ORDER BY EndUtc` works as it should)
and is human-readable in manual queries.

*Instead:* Unix time as a number — more compact and faster, but the console would show
`1760000000` instead of a date, and the application is all about manual queries to its own database.

### Queries filter by `EndUtc`, not by `LocalDate`

The day boundary is configurable, and a `LocalDate` value written under old settings
knows nothing about the new ones. For the history to be recalculated under a new rule as a whole,
the only source of truth has to be the moment in UTC.

The `LocalDate` column is kept for convenient manual queries (`WHERE LocalDate LIKE '2026%'`)
and for reference — the application does not select anything by it.

*Instead:* drop the column altogether. Rejected: it does no harm, and the queries in the README
read better with it.

### `Pooling = false` in the connection string

With pooling on, `Dispose` does not close the file but returns the connection to the pool, and the database
can be neither deleted nor copied right after the application closes. There is a single connection here
that lives for the whole run, so the pool has nothing to gain.

The tests found this: deleting the temporary folder failed with "the file is being used by another process".

### Type case is compared in C#, not in SQL

SQLite's built-in `COLLATE NOCASE` folds case only for the Latin `A-Z`.
For Cyrillic it is useless: «созвон» and «СОЗВОН» are treated as different words.

*Instead:* `UNIQUE COLLATE NOCASE` in the schema — looks neater, but does not work
for Russian names, that is, for all the real ones.

### History is not rewritten when shown

Bringing a type to its spelling from the catalog happens **only when reading for the screen**.
The database keeps exactly what was put into it.

*Instead:* normalize the records once at startup. Rejected: silently editing
history is the last thing you expect from a tracker. If you want it, it takes a single
command in the console:
`UPDATE WorkSessions SET Type = 'Работа' WHERE lower(Type) = 'работа';`

### Deleting an entry — without confirmation

The trash icon appears only when hovering over a row; a dialog for every action
in a small list is more annoying than a rare mistake.

*Cost:* a deletion cannot be undone. This is a deliberate trade-off, and it is noted in the README.

---

## Domain

### The timer does not tick by itself

`PomodoroTimer` can only count and answer questions; ticks are sent to it by
`TimerViewModel`, and time comes from `TimeProvider`.

Thanks to this, a full cycle with pauses is checked by a test instantly, without a single
real second of waiting.

*Instead:* a `DispatcherTimer` inside the domain (as it used to be) — then the domain class
drags WPF along and cannot be tested at all.

### Two sources of time: `TimeProvider` and `ITicker`

This is not duplication. `TimeProvider` answers "how much time has passed" — a domain question.
`ITicker` answers "when to recompute the screen" — a UI question. The split
makes it possible to change the redraw rate without touching the accuracy of the countdown.

### The planned duration goes into the history

A tick comes every 100 ms, so the "exactly zero" moment is always overshot.
If the measured value were written, the database would accumulate 1500.07 seconds instead of 1500.

On an early stop the measured value is written — the planned duration is meaningless there.

### `Advance()` returns a `TimerStep` instead of raising events

The calling code reads top to bottom in one piece, and a test simply compares the result.

*Instead:* `WorkFinished`/`RestFinished` events on the timer itself — that would mean
subscribing and keeping track of the call order.

### An empty type and "No type" are different keys

The grouping key of an entry without a type is an empty string; the label is substituted only
when shown. Otherwise a type the user happened to name "No type" would merge
with the truly empty ones.

Just in case, such a name (and a dash) is simply not allowed into the catalog.

---

## Statistics

### The median is computed over worked days only

In a month with six working days out of thirty, the median over all days is zero
and says nothing. The interesting question is "how much do I usually work on a working day".

*Instead:* the median over all days of the period — more honest arithmetically, more useless in practice.
The decision is debatable, and that is exactly why it is recorded here.

### The median returns `long?`

Zero is a legitimate value: an entry's duration can be corrected to 0 minutes. So
"no data" and "it came out as zero" differ by type, not by value.

### A color is tied to the type's position in the catalog

The catalog order is the order of addition, and it is stable. If types were sorted
by total hours, colors would jump on every period switch; if alphabetically,
they would be repainted whenever a new type is added.

### The legend shows disabled types

Otherwise a disabled type would disappear from the screen and there would be nothing to turn it back on with.
That is why `StatsCalculator` computes the list of types **before** applying the filter.

### Shift+click on the last enabled type turns everything off

The screen shows zero hours and "Nothing was worked in this period". This is left
literal: the rule "Shift toggles one type" works without exceptions, and the state
is obvious and easily undone by the next click.

### Segment order in a bar — from longest to shortest

Large chunks at the bottom, small ones on top: the bar is less noisy and easier to read.

---

## Interface

### MVVM without a library

`Mvvm/` is four files of about a hundred lines in total: a base class with notifications,
two commands and a tick source.

*Instead:* `CommunityToolkit.Mvvm` with source generators. Rejected for the sake of readability:
in a project that should be understandable when read top to bottom, an attribute hiding
generated code works against the goal. It would have saved tens of lines,
not hundreds.

### Screen state is a boolean property, not `Visibility`

"Which screen is open" used to be found out by checking `StatsView.Visibility == Visible`
in five places in the code. Now it is `MainViewModel.IsStatsVisible`, and visibility is derived
from it by the standard converter.

### Commands re-evaluate their availability explicitly

`RaiseCanExecuteChanged()` is called where the state has changed.

*Instead:* `CommandManager.RequerySuggested` — WPF would poll the commands itself on every
user action. It works "magically" and hides who actually enables the button.

### A legend chip is a `Button`, not a `ToggleButton`

`ToggleButton` toggles itself before the model decides what to do with the filter,
and shows something untrue for a frame. Here the enabled state is drawn by a `DataTrigger` from the data.

### The view model knows about `Keyboard.Modifiers`

`ToggleTypeCommand` finds out by itself whether Shift is held. The view model layer already knows about WPF
(it holds the brushes for the chart), and in return the `ToggleType(type, shiftHeld)` method stays
ordinary and is tested directly.

*Instead:* a handler in the code-behind — that would mean keeping code where you
don't expect it, for the sake of a single flag.

### The list is not rebuilt after a row is edited

Otherwise the row that was just edited would slide out from under the cursor. Rebuilding
happens only when the set of rows changes: reload, deletion, legend filter.

### Row fields are filled directly in the constructor

Assigning through properties would look like an edit and would be written to the database on the very first
render of the list. This was a real bug in the previous version, and there is a test for it.

### The chart size comes from the code-behind

`ActualWidth` is read-only, there is nothing to bind it with. There are two handlers
(`SizeChanged` and `DataContextChanged`) because the size and the model arrive
in an unpredictable order; with `SizeChanged` alone the chart was once built
with zero width and stayed that way.

*Instead:* an attached observer property — forty lines of infrastructure for
the same result.

### All colors live in one dictionary

There is not a single hex value in the markup. There used to be seventeen,
two of them almost identical (`#FF262A35` and `#FF272A35`) — a difference of one
in a single channel, indistinguishable to the eye and almost certainly accidental.

### Column widths — via `SharedSizeGroup`

The header and the rows belong to the same groups and cannot drift apart. Widths used to
be written as numbers in four places.

---

## Languages

### Strings are an abstract class, not `.resx`

A missing translation must be a build error. All labels in `Strings` are declared
abstract, so a new key won't let the build pass until both languages translate it.

*Instead:* `.resx` resource files — the standard .NET way, with tooling for
translators and satellite assemblies. Rejected: there are no translators here, and a missing
key in `.resx` silently returns an empty string, and only at run time.

### The language is chosen at build time, not in the settings

The Russian and English versions are two separate `.exe` files. A language drop-down in the menu
under the gear looked out of place, and two builds are more honest: a person downloads their own
and never sees the other one.

The side benefit turned out bigger than expected: the code lost the language-change event,
the subscriptions to it in five models, the `Language` setting in the database, the `LanguageOption` type
and the rebuilding of the period list. Switching on the fly cost noticeably more than
it seemed until it was removed.

*Cost:* to change the language you have to download the other file. The database is shared — both
versions read and write the same schema.

### The language constant is added by a target, not a conditional `PropertyGroup`

`AppLanguage` comes from the publish profile, and the profile is imported **after**
the body of the `.csproj` has been evaluated. A condition in a `PropertyGroup` won't see it: that is exactly why
the first English build came out Russian. The `AddLanguageConstant` target
runs after all the imports.

### Dates stayed European in both languages

`16.10.2026` in English too. The application is personal, the format is familiar, and translating dates
would have doubled the formatting tests without giving anything.

### The period list is not rebuilt when the language changes

The items stay the same objects, only their labels change. The list cannot be
swapped: `ComboBox` won't find the selected item in the new list and will set it to `null`,
and the whole screen is computed from an empty period. That is exactly how the statistics screen
once crashed — there is a test for it now.

### The reserved "no type" name is forbidden in both languages at once

`WorkTypes.ReservedNames` does not depend on the current language. Otherwise a type created
in the English interface as «Без типа» (the Russian "No type"), after switching to Russian, would merge
with the bucket for entries without a type.

---

## Project

### Everything is `internal`

This is an application, not a library; nothing is exposed to the outside. Tests see internal
types through `InternalsVisibleTo`.

A side benefit: the `CA1062` rule (`null` checks on arguments) is turned off
with good reason — calls from outside the assembly simply never happen.

### Warnings are build errors

"It built" means "it built cleanly". Three rules are turned off in `.editorconfig`
with explanations, four places are suppressed individually with an attribute and a justification.

### There is no XML documentation

Explanations live in `docs/` and in short comments where the behavior is not obvious.
A doc comment above `public string Type { get; }` that retells
the property name adds nothing and is the first thing to go stale.

### Comments answer "why"

If a comment retells the code, it is redundant. If it explains why a non-obvious
approach was chosen, it is needed, and there are about thirty of those in the project.

### Tests live inside the project folder

`tests/Pomidoro.Tests/`, not as a separate tree alongside. This keeps the repository
flat and the path to the `.exe` unchanged.

*Cost:* `Pomidoro.csproj` has to exclude `tests\**` from compilation explicitly,
otherwise the SDK would pull those files into the main project.

### WinForms in a WPF project

Only for `NotifyIcon` — WPF has no tray icon of its own. The implicit WinForms `using`s
must be removed, otherwise types with the same names clash all over the code.

### The icon and the sound are drawn in memory

Neither a `.ico` nor a `.wav` is needed next to the application, and the user's system sounds
play no part. For single-file publishing it also means one file instead of three.

### The developer console exists at all

A tool for myself: look at the history, fix a typo, check a hypothesis.
There are deliberately no checks in it — the warning is shown right in the window.

*Cost:* you can break your own database with it. That is accepted.
