using Pomidoro.Domain;
using Pomidoro.Tests.Fakes;
using Xunit;

namespace Pomidoro.Tests;

public class PomodoroTimerTests
{
    private static readonly TimeSpan Work = TimeSpan.FromMinutes(25);
    private static readonly TimeSpan Rest = TimeSpan.FromMinutes(5);

    private readonly FakeTime _time = new(new DateTimeOffset(2026, 10, 16, 9, 0, 0, TimeSpan.Zero));
    private readonly PomodoroTimer _timer;

    public PomodoroTimerTests()
    {
        _timer = new PomodoroTimer(_time);
    }

    [Fact]
    public void Starts_idle()
    {
        Assert.True(_timer.IsIdle);
        Assert.False(_timer.IsPaused);
        Assert.Equal(TimeSpan.Zero, _timer.Elapsed);
    }

    [Fact]
    public void Work_counts_down_while_time_passes()
    {
        _timer.Start(Work, Rest);
        _time.Advance(TimeSpan.FromMinutes(10));

        Assert.Equal(TimeSpan.FromMinutes(10), _timer.Elapsed);
        Assert.Equal(TimeSpan.FromMinutes(15), _timer.Remaining);
        Assert.Equal(0.4, _timer.Progress, 3);
    }

    [Fact]
    public void Second_start_is_ignored_while_running()
    {
        _timer.Start(Work, Rest);
        _time.Advance(TimeSpan.FromMinutes(10));
        _timer.Start(TimeSpan.FromMinutes(1), Rest);

        Assert.Equal(Work, _timer.PhaseLength);
        Assert.Equal(TimeSpan.FromMinutes(10), _timer.Elapsed);
    }

    [Fact]
    public void Paused_time_does_not_count()
    {
        _timer.Start(Work, Rest);
        _time.Advance(TimeSpan.FromMinutes(10));

        _timer.Pause();
        _time.Advance(TimeSpan.FromHours(3));

        Assert.True(_timer.IsPaused);
        Assert.Equal(TimeSpan.FromMinutes(10), _timer.Elapsed);

        _timer.Resume();
        _time.Advance(TimeSpan.FromMinutes(5));

        Assert.False(_timer.IsPaused);
        Assert.Equal(TimeSpan.FromMinutes(15), _timer.Elapsed);
    }

    [Fact]
    public void Finished_work_is_recorded_with_its_planned_length_and_rest_starts()
    {
        _timer.Start(Work, Rest);

        // Тик пришёл позже нуля, но в историю должно попасть ровно 25 минут.
        _time.Advance(Work + TimeSpan.FromMilliseconds(80));
        TimerStep step = _timer.Advance();

        Assert.Equal(TimerEvent.WorkFinished, step.Event);
        Assert.NotNull(step.Completed);
        Assert.Equal(25 * 60, step.Completed.DurationSec);
        Assert.Equal(_time.GetUtcNow().UtcDateTime, step.Completed.EndUtc);
        Assert.Equal(Rest, _timer.PhaseLength);
        Assert.False(_timer.IsIdle);
    }

    [Fact]
    public void Finished_rest_returns_the_timer_to_idle_without_a_record()
    {
        _timer.Start(Work, Rest);
        _time.Advance(Work);
        _timer.Advance();

        _time.Advance(Rest);
        TimerStep step = _timer.Advance();

        Assert.Equal(TimerEvent.RestFinished, step.Event);
        Assert.Null(step.Completed);
        Assert.True(_timer.IsIdle);
    }

    [Fact]
    public void Advance_does_nothing_before_the_phase_ends()
    {
        _timer.Start(Work, Rest);
        _time.Advance(TimeSpan.FromMinutes(24));

        Assert.Equal(TimerEvent.Nothing, _timer.Advance().Event);
    }

    [Fact]
    public void Advance_does_nothing_while_paused_past_the_end()
    {
        _timer.Start(Work, Rest);
        _time.Advance(TimeSpan.FromMinutes(10));
        _timer.Pause();
        _time.Advance(TimeSpan.FromHours(1));

        Assert.Equal(TimerEvent.Nothing, _timer.Advance().Event);
    }

    [Fact]
    public void Interrupted_work_is_recorded_with_the_time_actually_worked()
    {
        _timer.Start(Work, Rest);
        _time.Advance(TimeSpan.FromMinutes(7));

        WorkSession? interrupted = _timer.Stop();

        Assert.NotNull(interrupted);
        Assert.Equal(7 * 60, interrupted.DurationSec);
        Assert.True(_timer.IsIdle);
    }

    [Fact]
    public void Accidental_click_records_nothing()
    {
        _timer.Start(Work, Rest);
        _time.Advance(TimeSpan.FromMilliseconds(300));

        Assert.Null(_timer.Stop());
    }

    [Fact]
    public void Interrupted_rest_records_nothing()
    {
        _timer.Start(Work, Rest);
        _time.Advance(Work);
        _timer.Advance();

        _time.Advance(TimeSpan.FromMinutes(2));

        Assert.Null(_timer.Stop());
        Assert.True(_timer.IsIdle);
    }
}
