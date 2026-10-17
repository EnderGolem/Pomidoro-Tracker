using Pomidoro.Domain;
using Pomidoro.Formatting;
using Pomidoro.Localization;
using Pomidoro.Mvvm;
using Pomidoro.Storage;

namespace Pomidoro.ViewModels;

// The "today" block: hours on top and the list of intervals below. No dates anywhere:
// this screen is always about the current logical day.
internal sealed class TodayViewModel : ObservableObject
{
    private readonly ISessionStore _store;
    private readonly AppSettings _settings;
    private readonly TimeProvider _time;

    private List<WorkSession> _sessions = [];
    private string _hoursText = string.Empty;
    private string _countText = string.Empty;

    public TodayViewModel(ISessionStore store, AppSettings settings, TimeProvider time)
    {
        _store = store;
        _settings = settings;
        _time = time;

        List = new SessionListViewModel(store, showDate: false, Loc.S.TodayEmpty);
        List.RowChanged += OnRowChanged;
        List.RowDeleted += OnRowDeleted;

        UpdateTotals();
    }

    public SessionListViewModel List { get; }

    public string HoursText
    {
        get => _hoursText;
        private set => Set(ref _hoursText, value);
    }

    public string CountText
    {
        get => _countText;
        private set => Set(ref _countText, value);
    }

    public void Reload()
    {
        DayRange today = DayRange.SingleDay(_settings.DayBoundary.Today(_time));

        _sessions = [.. _store.GetRange(today, _settings.DayBoundary)];
        List.Load(_sessions, _store.GetTypeCatalog());
        UpdateTotals();
    }

    // The list isn't rebuilt: the edit came from the list itself, and the row under the cursor
    // would jump away right in the middle of editing.
    private void OnRowChanged(object? sender, SessionRowViewModel row)
    {
        int index = _sessions.FindIndex(session => session.Id == row.Id);

        if (index >= 0)
        {
            _sessions[index] = _sessions[index] with { DurationSec = row.DurationSec, Type = row.Type };
        }

        UpdateTotals();
    }

    private void OnRowDeleted(object? sender, SessionRowViewModel row)
    {
        _store.Delete(row.Id);
        Reload();
    }

    private void UpdateTotals()
    {
        HoursText = Loc.S.Hours(Format.HoursNumber(_sessions.Sum(session => (long)session.DurationSec)));
        CountText = Loc.S.Pomodoros(_sessions.Count);
    }
}
