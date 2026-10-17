using System.Globalization;
using System.Windows.Input;
using Pomidoro.Domain;
using Pomidoro.Formatting;
using Pomidoro.Mvvm;
using Pomidoro.Storage;

namespace Pomidoro.ViewModels;

// A pomodoro list row. Minutes and type are edited in place and go straight to the database.
internal sealed class SessionRowViewModel : ObservableObject
{
    public const int MinMinutes = 0;
    public const int MaxMinutes = 1440;

    private readonly ISessionStore _store;

    private string _minutes;
    private string _selectedOption;

    public SessionRowViewModel(
        WorkSession session,
        IReadOnlyList<string> typeCatalog,
        bool showDate,
        ISessionStore store)
    {
        _store = store;

        Id = session.Id;
        DurationSec = session.DurationSec;
        Type = WorkTypes.Canonical(session.Type, typeCatalog);
        ShowsDate = showDate;
        Date = showDate ? Format.DayAndMonth(DateOnly.FromDateTime(session.EndLocal)) : string.Empty;
        EndTime = Format.TimeOfDay(session.EndLocal);

        // Fields are set directly: going through the properties would look like an edit
        // and be written to the database on the row's very first render.
        _minutes = Format.Minutes(session.Duration);
        _selectedOption = Type.Length == 0 ? WorkTypes.NoTypeOption : Type;

        List<string> options = [WorkTypes.NoTypeOption, .. typeCatalog];

        // The type may have been removed from the catalog after the record was tagged with it:
        // it stays in history, and it must still be possible to select it again.
        if (Type.Length > 0 && !typeCatalog.Contains(Type, WorkTypes.Comparer))
        {
            options.Add(Type);
        }

        TypeOptions = options;
        DeleteCommand = new RelayCommand(() => DeleteRequested?.Invoke(this, EventArgs.Empty));
    }

    public event EventHandler? Changed;

    public event EventHandler? DeleteRequested;

    public long Id { get; }

    public int DurationSec { get; private set; }

    public string Type { get; private set; }

    public bool ShowsDate { get; }

    public string Date { get; }

    public string EndTime { get; }

    public IReadOnlyList<string> TypeOptions { get; }

    public ICommand DeleteCommand { get; }

    // Garbage and out-of-range values silently roll back to the previous value: complaining
    // with a dialog about every typo in the list would be too much.
    public string Minutes
    {
        get => _minutes;
        set
        {
            if (!TryParseMinutes(value, out int minutes))
            {
                Raise();
                return;
            }

            string normalized = Format.Integer(minutes);

            if (normalized == _minutes)
            {
                Raise();
                return;
            }

            _minutes = normalized;
            DurationSec = minutes * 60;
            _store.UpdateDuration(Id, DurationSec);
            Raise();
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    public string SelectedOption
    {
        get => _selectedOption;
        set
        {
            string type = value == WorkTypes.NoTypeOption ? string.Empty : value;

            if (!Set(ref _selectedOption, value) || type == Type)
            {
                return;
            }

            Type = type;
            _store.UpdateType(Id, type);
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    private static bool TryParseMinutes(string? text, out int minutes) =>
        int.TryParse(text?.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out minutes) &&
        minutes is >= MinMinutes and <= MaxMinutes;
}
