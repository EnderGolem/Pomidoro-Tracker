using System.Windows.Input;
using Pomidoro.Domain;
using Pomidoro.Mvvm;
using Pomidoro.Storage;

namespace Pomidoro.ViewModels;

// Сшивает экраны вместе: какой из них виден, кто перечитывает базу после правок
// и куда уходит завершённая помидорка.
internal sealed class MainViewModel : ObservableObject
{
    private readonly ISessionStore _store;

    private bool _isStatsVisible;
    private bool _isSettingsOpen;

    public MainViewModel(ISessionStore store, AppSettings settings, TimeProvider time, ITicker ticker)
    {
        _store = store;

        Timer = new TimerViewModel(settings, time, ticker);
        Today = new TodayViewModel(store, settings, time);
        Stats = new StatsViewModel(store, settings, time);
        Settings = new SettingsViewModel(store, settings);

        Timer.WorkCompleted += OnWorkCompleted;

        Settings.CatalogChanged += (_, _) => ReloadVisibleScreen();
        Settings.SqlConsoleRequested += (_, _) => OpenSqlConsole();
        settings.Changed += (_, _) => ReloadVisibleScreen();

        ShowStatsCommand = new RelayCommand(() => IsStatsVisible = true);
        ShowTimerCommand = new RelayCommand(() => IsStatsVisible = false);
        ToggleSettingsCommand = new RelayCommand(() => IsSettingsOpen = !IsSettingsOpen);

        Today.Reload();
    }

    public event EventHandler? SqlConsoleRequested;

    public TimerViewModel Timer { get; }

    public TodayViewModel Today { get; }

    public StatsViewModel Stats { get; }

    public SettingsViewModel Settings { get; }

    public ICommand ShowStatsCommand { get; }

    public ICommand ShowTimerCommand { get; }

    public ICommand ToggleSettingsCommand { get; }

    public bool IsStatsVisible
    {
        get => _isStatsVisible;
        private set
        {
            if (!Set(ref _isStatsVisible, value))
            {
                return;
            }

            Raise(nameof(IsTimerVisible));
            IsSettingsOpen = false;
            ReloadVisibleScreen();
        }
    }

    public bool IsTimerVisible => !_isStatsVisible;

    public bool IsSettingsOpen
    {
        get => _isSettingsOpen;
        set => Set(ref _isSettingsOpen, value);
    }

    // Окно могло провисеть в трее до следующего дня — тогда «сегодня» уже другое.
    public void ReloadVisibleScreen()
    {
        if (_isStatsVisible)
        {
            Stats.Reload();
        }
        else
        {
            Today.Reload();
        }
    }

    public void ReloadAfterSql()
    {
        Settings.ReloadFromStore();
        ReloadVisibleScreen();
    }

    // Меню настроек — отдельное окно системы, и Hide() главного окна его не прячет.
    public void OnHiddenToTray()
    {
        IsSettingsOpen = false;
    }

    private void OnWorkCompleted(object? sender, WorkSession session)
    {
        _store.Add(session);
        ReloadVisibleScreen();
    }

    private void OpenSqlConsole()
    {
        IsSettingsOpen = false;
        SqlConsoleRequested?.Invoke(this, EventArgs.Empty);
    }
}
