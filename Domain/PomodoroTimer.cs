namespace Pomidoro.Domain;

internal sealed class PomodoroTimer
{
    public const int MinRecordedSeconds = 1;

    private readonly TimeProvider _time;

    private TimeSpan _elapsedBeforePause;
    private long? _runningSince;
    private TimeSpan _restLength;

    public PomodoroTimer(TimeProvider time)
    {
        _time = time;
    }

    public PomodoroPhase Phase { get; private set; } = PomodoroPhase.Idle;

    public TimeSpan PhaseLength { get; private set; }

    public bool IsIdle => Phase == PomodoroPhase.Idle;

    public bool IsPaused => !IsIdle && _runningSince is null;

    public TimeSpan Elapsed => _runningSince is long since
        ? _elapsedBeforePause + _time.GetElapsedTime(since)
        : _elapsedBeforePause;

    public TimeSpan Remaining
    {
        get
        {
            TimeSpan left = PhaseLength - Elapsed;

            return left > TimeSpan.Zero ? left : TimeSpan.Zero;
        }
    }

    public double Progress => PhaseLength > TimeSpan.Zero
        ? Math.Clamp(Elapsed / PhaseLength, 0.0, 1.0)
        : 0.0;

    public void Start(TimeSpan workLength, TimeSpan restLength)
    {
        if (!IsIdle)
        {
            return;
        }

        _restLength = restLength;
        EnterPhase(PomodoroPhase.Work, workLength);
    }

    public void Pause()
    {
        if (IsIdle || IsPaused)
        {
            return;
        }

        _elapsedBeforePause = Elapsed;
        _runningSince = null;
    }

    public void Resume()
    {
        if (IsIdle || !IsPaused)
        {
            return;
        }

        _runningSince = _time.GetTimestamp();
    }

    public WorkSession? Stop()
    {
        if (IsIdle)
        {
            return null;
        }

        PomodoroPhase stopped = Phase;
        TimeSpan worked = Elapsed;

        ResetToIdle();

        return stopped == PomodoroPhase.Work && worked.TotalSeconds >= MinRecordedSeconds
            ? NewSession(worked)
            : null;
    }

    public TimerStep Advance()
    {
        if (IsIdle || IsPaused || Remaining > TimeSpan.Zero)
        {
            return TimerStep.Nothing;
        }

        if (Phase == PomodoroPhase.Rest)
        {
            ResetToIdle();

            return new TimerStep(TimerEvent.RestFinished, null);
        }

        // Длительность берём плановую, а не измеренную: тик мог прийти на десятки
        // миллисекунд позже нуля, и в истории это выглядело бы как 25:00,08.
        WorkSession completed = NewSession(PhaseLength);
        EnterPhase(PomodoroPhase.Rest, _restLength);

        return new TimerStep(TimerEvent.WorkFinished, completed);
    }

    private void EnterPhase(PomodoroPhase phase, TimeSpan length)
    {
        Phase = phase;
        PhaseLength = length;
        _elapsedBeforePause = TimeSpan.Zero;
        _runningSince = _time.GetTimestamp();
    }

    private void ResetToIdle()
    {
        Phase = PomodoroPhase.Idle;
        PhaseLength = TimeSpan.Zero;
        _elapsedBeforePause = TimeSpan.Zero;
        _runningSince = null;
    }

    private WorkSession NewSession(TimeSpan worked) => new()
    {
        EndUtc = _time.GetUtcNow().UtcDateTime,
        DurationSec = (int)Math.Round(worked.TotalSeconds)
    };
}
