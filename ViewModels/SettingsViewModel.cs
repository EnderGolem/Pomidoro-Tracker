using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows.Input;
using Pomidoro.Domain;
using Pomidoro.Formatting;
using Pomidoro.Localization;
using Pomidoro.Mvvm;
using Pomidoro.Storage;

namespace Pomidoro.ViewModels;

// The menu under the gear. Everything entered here is validated on the spot and goes
// straight to the database; garbage rolls back to the previous value.
internal sealed class SettingsViewModel : ObservableObject
{
    private readonly ISessionStore _store;
    private readonly AppSettings _settings;

    private string _newTypeName = string.Empty;

    public SettingsViewModel(ISessionStore store, AppSettings settings)
    {
        _store = store;
        _settings = settings;

        AddTypeCommand = new RelayCommand(AddType);
        RemoveTypeCommand = new RelayCommand<string>(RemoveType);
        OpenSqlConsoleCommand = new RelayCommand(() => SqlConsoleRequested?.Invoke(this, EventArgs.Empty));

        ReloadFromStore();
    }

    public event EventHandler? CatalogChanged;

    public event EventHandler? SqlConsoleRequested;

    public ObservableCollection<string> Types { get; } = [];

    public ICommand AddTypeCommand { get; }

    public ICommand RemoveTypeCommand { get; }

    public ICommand OpenSqlConsoleCommand { get; }

    public string WorkMinutes
    {
        get => Format.Integer(_settings.WorkMinutes);
        set => SaveMinutes(value, minutes => _settings.WorkMinutes = minutes);
    }

    public string RestMinutes
    {
        get => Format.Integer(_settings.RestMinutes);
        set => SaveMinutes(value, minutes => _settings.RestMinutes = minutes);
    }

    public string DayBoundaryHour
    {
        get => Format.Integer(_settings.DayBoundaryHour);
        set
        {
            if (TryParse(value, out int hour) && hour is >= DayBoundary.MinHour and <= DayBoundary.MaxHour)
            {
                _settings.DayBoundaryHour = hour;
            }

            Raise();
            Raise(nameof(DayBoundaryHint));
        }
    }

    public string DayBoundaryHint => Loc.S.DayBoundaryHint(_settings.DayBoundaryHour);

    public string NewTypeName
    {
        get => _newTypeName;
        set => Set(ref _newTypeName, value);
    }

    public bool HasNoTypes => Types.Count == 0;

    public void ReloadFromStore()
    {
        Types.Clear();

        foreach (string name in _store.GetTypeCatalog())
        {
            Types.Add(name);
        }

        Raise(nameof(HasNoTypes));
        Raise(nameof(WorkMinutes));
        Raise(nameof(RestMinutes));
        Raise(nameof(DayBoundaryHour));
        Raise(nameof(DayBoundaryHint));
    }

    private static bool TryParse(string? text, out int value) =>
        int.TryParse(text?.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out value);

    private void SaveMinutes(string? text, Action<int> apply)
    {
        if (TryParse(text, out int minutes) && AppSettings.IsValidMinutes(minutes))
        {
            apply(minutes);
        }

        Raise();
    }

    private void AddType()
    {
        _store.AddTypeToCatalog(NewTypeName);
        NewTypeName = string.Empty;
        ReloadFromStore();
        CatalogChanged?.Invoke(this, EventArgs.Empty);
    }

    private void RemoveType(string name)
    {
        _store.RemoveTypeFromCatalog(name);
        ReloadFromStore();
        CatalogChanged?.Invoke(this, EventArgs.Empty);
    }
}
