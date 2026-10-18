# Pomodoro tracker

A desktop Pomodoro timer for Windows. Every interval you work is saved to a local
SQLite database and shows up in the statistics: by day, by work type, and as a single
total for the period.

No accounts, no internet, no installation.

## Features

- Work and rest timer with adjustable durations.
- Today's list: total hours, pomodoro count, inline editing.
- Weekly and monthly statistics: a per-day chart broken down by type, filters, median.
- Adjustable day boundary, so late-night work counts towards the previous day.
- Runs from the tray: the window is closed, the timer keeps going, the chime still plays.
- SQL console for your own queries against the database.
- Russian and English versions: two separate builds that share one database.

## Building a single-file exe

To build, you need Windows and the [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9.0).

Run from the project folder.

English version:

```
dotnet publish Pomidoro.csproj -p:PublishProfile=SingleFile-En
```

Russian version:

```
dotnet publish Pomidoro.csproj -p:PublishProfile=SingleFile-Ru
```

The result is a single file, `publish\en\Pomidoro.exe` or `publish\ru\Pomidoro.exe`,
about 72 MB. .NET is packed inside, so nothing needs to be installed on the computer
where it runs — any 64-bit Windows will do.

## First run

Put `Pomidoro.exe` in a folder of its own and run it.

On first run the app creates `pomodoro.db` next to the exe. It holds your whole history
and your settings, so the folder must be writable.

To move your history to another computer or back it up, copy the folder.

## How to use it

**Start** begins work. When the time is up, a chime plays and rest starts right away.
After the second chime the timer is ready again.

- **Pause** stops the countdown. Paused time is not counted.
- **Stop** ends the interval. If you were working, the minutes you already put in are still saved.
- **The close button** hides the window to the tray; the timer keeps running. On Windows 11
  the icon is usually tucked away under the arrow in the corner of the taskbar. To quit
  for real, right-click the icon → **Exit**.

Below the timer is the list of today's pomodoros. To fix the minutes or the type in a row,
just click them. Hovering over a row shows a trash icon: the entry is deleted immediately,
with no confirmation and no undo.

## Statistics

The **Statistics** button at the top of the window. You can pick the current or previous
week, or the current or previous month.

The chart has a bar for every day, coloured by work type. Weekends are dimmed.
The types under the chart work as filters:

- clicking a type shows only that type;
- clicking it again brings all of them back;
- Shift+click adds or removes one type without touching the others.

The median only counts days on which you worked. Otherwise, in a month with six working
days, it would always be zero.

## Settings

The gear at the top of the window.

- **Duration** of work and rest, in minutes. New values apply from the next timer start.
- **Types** — your own labels, such as "Work" or "Study". A type is picked in a list row.
  Removing a type from the settings does not remove it from past entries.
- **Day boundary.** If you work past midnight, set it to, say, `4`:
  everything that ends before 4 a.m. counts towards the previous day.

Settings are saved immediately; there is nothing to confirm.

## SQL console

Gear → **Developer SQL**. Query your own data: how many hours this year, which weekdays
you work most, and so on. Ready-made examples are
in [docs/03-storage.md](docs/03-storage.md#useful-queries).

`F5` or `Ctrl+Enter` runs the query; if something is selected, only the selection runs.

**Careful:** queries run without any checks. `DELETE` and `DROP` do exactly what they say,
and the history cannot be brought back. Copy `pomodoro.db` before experimenting.

## For developers

How the code is organized, why it looks the way it does, and how to change it —
see [docs/](docs/README.md).
