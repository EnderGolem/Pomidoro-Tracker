using Pomidoro.Domain;

namespace Pomidoro.Storage;

// Настройки читаются один раз при запуске и пишутся сразу при изменении,
// поэтому терять их на аварийном выходе нечего.
internal sealed class AppSettings
{
    public const int MinMinutes = 1;
    public const int MaxMinutes = 600;
    public const int DefaultWorkMinutes = 25;
    public const int DefaultRestMinutes = 5;

    private const string WorkMinutesKey = "WorkMinutes";

    // Ключ остался от прежнего названия фазы: в уже существующих базах лежит именно он.
    private const string RestMinutesKey = "BreakMinutes";
    private const string DayBoundaryKey = "DayBoundaryHour";

    private readonly ISessionStore _store;

    private int _workMinutes;
    private int _restMinutes;
    private int _dayBoundaryHour;

    public AppSettings(ISessionStore store)
    {
        _store = store;
        _workMinutes = ClampMinutes(store.GetSetting(WorkMinutesKey, DefaultWorkMinutes));
        _restMinutes = ClampMinutes(store.GetSetting(RestMinutesKey, DefaultRestMinutes));
        _dayBoundaryHour = new DayBoundary(store.GetSetting(DayBoundaryKey, DayBoundary.DefaultHour)).Hour;
    }

    public event EventHandler? Changed;

    public int WorkMinutes
    {
        get => _workMinutes;
        set => Save(WorkMinutesKey, ref _workMinutes, ClampMinutes(value));
    }

    public int RestMinutes
    {
        get => _restMinutes;
        set => Save(RestMinutesKey, ref _restMinutes, ClampMinutes(value));
    }

    public int DayBoundaryHour
    {
        get => _dayBoundaryHour;
        set => Save(DayBoundaryKey, ref _dayBoundaryHour, new DayBoundary(value).Hour);
    }

    public DayBoundary DayBoundary => new(_dayBoundaryHour);

    public static bool IsValidMinutes(int minutes) => minutes is >= MinMinutes and <= MaxMinutes;

    private static int ClampMinutes(int minutes) => Math.Clamp(minutes, MinMinutes, MaxMinutes);

    private void Save(string key, ref int field, int value)
    {
        if (field == value)
        {
            return;
        }

        field = value;
        _store.SetSetting(key, value);
        Changed?.Invoke(this, EventArgs.Empty);
    }
}
