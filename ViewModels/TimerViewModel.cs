using System.Windows.Input;
using Pomidoro.Domain;
using Pomidoro.Formatting;
using Pomidoro.Localization;
using Pomidoro.Mvvm;
using Pomidoro.Storage;

namespace Pomidoro.ViewModels;

internal sealed class TimerViewModel : ObservableObject
{
    private readonly PomodoroTimer _timer;
    private readonly AppSettings _settings;
    private readonly ITicker _ticker;

    private string _timeText = string.Empty;
    private string _phaseText = string.Empty;
    private string _hintText = string.Empty;
    private string _pauseText = string.Empty;
    private double _progress;

    public TimerViewModel(AppSettings settings, TimeProvider time, ITicker ticker)
    {
        _settings = settings;
        _ticker = ticker;
        _timer = new PomodoroTimer(time);

        _ticker.Tick += (_, _) => Advance();
        _settings.Changed += (_, _) => Refresh();

        StartCommand = new RelayCommand(Start, () => _timer.IsIdle);
        PauseCommand = new RelayCommand(PauseOrResume, () => !_timer.IsIdle);
        StopCommand = new RelayCommand(Stop, () => !_timer.IsIdle);

        Refresh();
    }

    public event EventHandler<WorkSession>? WorkCompleted;

    public event EventHandler? PhaseFinished;

    public RelayCommand StartCommand { get; }

    public RelayCommand PauseCommand { get; }

    public RelayCommand StopCommand { get; }

    public bool IsWorking => _timer.Phase == PomodoroPhase.Work;

    public bool IsResting => _timer.Phase == PomodoroPhase.Rest;

    public bool IsPaused => _timer.IsPaused;

    public string TimeText
    {
        get => _timeText;
        private set => Set(ref _timeText, value);
    }

    public string PhaseText
    {
        get => _phaseText;
        private set => Set(ref _phaseText, value);
    }

    public string HintText
    {
        get => _hintText;
        private set => Set(ref _hintText, value);
    }

    public string PauseText
    {
        get => _pauseText;
        private set => Set(ref _pauseText, value);
    }

    public double Progress
    {
        get => _progress;
        private set => Set(ref _progress, value);
    }

    public void Advance()
    {
        TimerStep step = _timer.Advance();

        if (step.Completed is WorkSession session)
        {
            WorkCompleted?.Invoke(this, session);
        }

        if (step.Event != TimerEvent.Nothing)
        {
            PhaseFinished?.Invoke(this, EventArgs.Empty);
        }

        Refresh();
    }

    private void Start()
    {
        _timer.Start(
            TimeSpan.FromMinutes(_settings.WorkMinutes),
            TimeSpan.FromMinutes(_settings.RestMinutes));

        _ticker.Start();
        Refresh();
    }

    private void PauseOrResume()
    {
        if (_timer.IsPaused)
        {
            _timer.Resume();
        }
        else
        {
            _timer.Pause();
        }

        Refresh();
    }

    private void Stop()
    {
        WorkSession? interrupted = _timer.Stop();

        if (interrupted is not null)
        {
            WorkCompleted?.Invoke(this, interrupted);
        }

        Refresh();
    }

    private void Refresh()
    {
        if (_timer.IsIdle)
        {
            _ticker.Stop();

            // When idle, the dial shows the configured work duration.
            TimeText = Format.Clock(TimeSpan.FromMinutes(_settings.WorkMinutes));
            PhaseText = Loc.S.PhaseIdle;
            HintText = Loc.S.HintIdle;
            PauseText = Loc.S.PauseButton;
            Progress = 0.0;
        }
        else
        {
            bool working = _timer.Phase == PomodoroPhase.Work;
            string phase = working ? Loc.S.PhaseWork : Loc.S.PhaseRest;

            TimeText = Format.Clock(_timer.Remaining);
            PhaseText = _timer.IsPaused ? phase + Loc.S.PausedSuffix : phase;
            PauseText = _timer.IsPaused ? Loc.S.ResumeButton : Loc.S.PauseButton;
            Progress = _timer.Progress;

            HintText = _timer.IsPaused
                ? Loc.S.HintPaused
                : working
                    ? Loc.S.HintWorking
                    : Loc.S.HintResting;
        }

        Raise(nameof(IsWorking));
        Raise(nameof(IsResting));
        Raise(nameof(IsPaused));

        StartCommand.RaiseCanExecuteChanged();
        PauseCommand.RaiseCanExecuteChanged();
        StopCommand.RaiseCanExecuteChanged();
    }
}
