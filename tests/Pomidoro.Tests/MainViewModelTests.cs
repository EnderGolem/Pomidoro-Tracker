using Pomidoro.Domain;
using Pomidoro.Storage;
using Pomidoro.Tests.Fakes;
using Pomidoro.ViewModels;
using Xunit;

namespace Pomidoro.Tests;

// The main model assembles the other four and wires them to the store.
// What's tested is the wiring: a finished pomodoro must reach the database,
// and leaving the timer screen must close the settings menu.
public class MainViewModelTests
{
    private readonly FakeSessionStore _store = new();
    private readonly FakeTime _time = new(new DateTimeOffset(2026, 10, 16, 12, 0, 0, TimeSpan.Zero));
    private readonly FakeTicker _ticker = new();
    private readonly MainViewModel _main;

    public MainViewModelTests()
    {
        _main = new MainViewModel(_store, new AppSettings(_store), _time, _ticker);
    }

    [Fact]
    public void Finished_pomodoro_reaches_the_store()
    {
        _main.Timer.StartCommand.Execute(null);

        Assert.True(_ticker.IsRunning);

        _time.Advance(TimeSpan.FromMinutes(AppSettings.DefaultWorkMinutes));
        _ticker.Fire();

        DayRange everything = new(new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31));
        WorkSession saved = Assert.Single(_store.GetRange(everything, DayBoundary.Default));

        Assert.Equal(AppSettings.DefaultWorkMinutes * 60, saved.DurationSec);
    }

    [Fact]
    public void Switching_to_stats_closes_the_settings_menu()
    {
        _main.IsSettingsOpen = true;

        _main.ShowStatsCommand.Execute(null);

        Assert.True(_main.IsStatsVisible);
        Assert.False(_main.IsSettingsOpen);
    }

    [Fact]
    public void Hiding_to_tray_closes_the_settings_menu()
    {
        _main.IsSettingsOpen = true;

        _main.OnHiddenToTray();

        Assert.False(_main.IsSettingsOpen);
    }
}
