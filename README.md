# Pomodoro tracker

A small Windows timer for the Pomodoro technique: work 25 minutes, rest 5, repeat.
Every finished interval is saved, so you can see how much you actually worked —
today, this week or this month.

No accounts, no internet, nothing to install.

## Getting started

1. Take `Pomidoro.exe` — Russian or English, they are separate files.
2. Put it in any folder and run it.

That's it. The history is kept in `pomodoro.db` next to the `.exe`.
To move to another computer or make a backup, copy the folder.

## How to use it

**Start** begins work. When the time is up, a chime plays and rest starts on its own.
After the second chime the timer is ready again.

- **Pause** stops the clock. Paused time is not counted.
- **Stop** ends the interval early. The minutes you already worked are still saved.
- **Closing the window** hides it to the tray — the timer keeps going.
  To quit, right-click the tray icon → **Exit**.

Under the timer is the list of today's pomodoros. Click the minutes or the type
in a row to correct it.

## Statistics

The **Statistics** button at the top. Shows the current or previous week or month:
a bar for each day, coloured by what you worked on.

Click a type under the chart to see only it. Click it again to see everything.
Shift+click adds or removes one type.

## Settings

The gear at the top of the window:

- **Work and rest duration** in minutes.
- **Types** — your own labels like "Work" or "Study", picked for each pomodoro.
- **Day boundary** — if you work past midnight, set it to e.g. `4`,
  and everything before 4 a.m. counts towards the previous day.

## Building from source

Requires the .NET 9 SDK.

```
dotnet build Pomidoro.sln -c Debug
dotnet publish Pomidoro.csproj -p:PublishProfile=SingleFile-Ru
```

The second command produces a single `publish\ru\Pomidoro.exe`
(`SingleFile-En` for the English one). How the code is built is in [docs/](docs/README.md).
