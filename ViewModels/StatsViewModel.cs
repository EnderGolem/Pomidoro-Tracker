using System.Collections.ObjectModel;
using System.Windows.Input;
using Pomidoro.Domain;
using Pomidoro.Formatting;
using Pomidoro.Localization;
using Pomidoro.Mvvm;
using Pomidoro.Stats;
using Pomidoro.Storage;

namespace Pomidoro.ViewModels;

internal sealed class StatsViewModel : ObservableObject
{
    // Подпись оси выше линии на половину своей высоты, иначе цифра съезжает вниз.
    private const double LabelHalfHeight = 7.0;

    private readonly ISessionStore _store;
    private readonly AppSettings _settings;
    private readonly TimeProvider _time;

    // Выключенные типы живут только в памяти: это взгляд на данные, а не настройка,
    // поэтому после перезапуска показываем снова всё.
    private readonly HashSet<string> _hiddenTypes = WorkTypes.NewSet();

    private List<WorkSession> _sessions = [];
    private IReadOnlyList<string> _typeCatalog = [];
    private StatsSummary? _summary;
    private DayRange _range;
    private double _plotWidth;
    private double _plotHeight = ChartLayout.MinPlotHeight;

    private PeriodOption _selectedPeriod;
    private string _rangeText = string.Empty;
    private string _hoursText = string.Empty;
    private string _countText = string.Empty;
    private string _medianText = string.Empty;
    private bool _isEmpty = true;

    public StatsViewModel(ISessionStore store, AppSettings settings, TimeProvider time)
    {
        _store = store;
        _settings = settings;
        _time = time;

        Periods = BuildPeriods();

        _selectedPeriod = Periods[0];
        _range = DayRange.SingleDay(settings.DayBoundary.Today(time));

        List = new SessionListViewModel(store, showDate: true, Loc.S.PeriodEmpty);
        List.RowChanged += OnRowChanged;
        List.RowDeleted += OnRowDeleted;

        ToggleTypeCommand = new RelayCommand<LegendItemViewModel>(item =>
            ToggleType(item.Type, Keyboard.Modifiers.HasFlag(ModifierKeys.Shift)));
    }

    public SessionListViewModel List { get; }

    public IReadOnlyList<PeriodOption> Periods { get; }

    public ObservableCollection<ChartColumnViewModel> Columns { get; } = [];

    public ObservableCollection<GridLineViewModel> GridLines { get; } = [];

    public ObservableCollection<LegendItemViewModel> Legend { get; } = [];

    public ICommand ToggleTypeCommand { get; }

    public PeriodOption SelectedPeriod
    {
        get => _selectedPeriod;
        set
        {
            // null приходит от ComboBox, когда он теряет выбранный элемент;
            // держать период пустым нельзя — на нём считается весь экран.
            if (value is null || !Set(ref _selectedPeriod, value))
            {
                return;
            }

            Reload();
        }
    }

    public string RangeText
    {
        get => _rangeText;
        private set => Set(ref _rangeText, value);
    }

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

    public string MedianText
    {
        get => _medianText;
        private set => Set(ref _medianText, value);
    }

    public bool IsEmpty
    {
        get => _isEmpty;
        private set => Set(ref _isEmpty, value);
    }

    public double ChartHeight => _plotHeight;

    public double GridWidth => ChartLayout.AxisWidth + _plotWidth;

    public void Reload()
    {
        _typeCatalog = _store.GetTypeCatalog();
        _range = StatsPeriods.Resolve(SelectedPeriod.Period, _settings.DayBoundary.Today(_time));
        _sessions = [.. _store.GetRange(_range, _settings.DayBoundary)];

        Refresh(rebuildList: true);
    }

    // Обычный клик оставляет только нажатый тип; повторный клик по единственному
    // включённому возвращает все; Shift+клик добавляет или убирает один тип.
    public void ToggleType(string type, bool shiftHeld)
    {
        List<string> allTypes = [.. Legend.Select(item => item.Type)];
        bool isShown = !_hiddenTypes.Contains(type);
        bool isOnlyShown = isShown &&
            allTypes.TrueForAll(other => WorkTypes.Comparer.Equals(other, type) || _hiddenTypes.Contains(other));

        if (shiftHeld)
        {
            if (isShown)
            {
                _hiddenTypes.Add(type);
            }
            else
            {
                _hiddenTypes.Remove(type);
            }
        }
        else if (isOnlyShown)
        {
            _hiddenTypes.Clear();
        }
        else
        {
            _hiddenTypes.Clear();
            _hiddenTypes.UnionWith(allTypes.Where(other => !WorkTypes.Comparer.Equals(other, type)));
        }

        Refresh(rebuildList: true);
    }

    // Размер панели меняется при каждом движении рамки окна, поэтому здесь только
    // пересчёт геометрии: ни запроса к базе, ни пересборки списка.
    public void SetPlotSize(double hostWidth, double hostHeight)
    {
        _plotWidth = ChartLayout.PlotWidth(hostWidth);
        _plotHeight = ChartLayout.PlotHeight(hostHeight);

        Raise(nameof(ChartHeight));
        Raise(nameof(GridWidth));
        BuildChart();
    }

    private static List<PeriodOption> BuildPeriods() => StatsPeriods.All
        .Select(period => new PeriodOption(period, Loc.S.PeriodTitle(period)))
        .ToList();

    private void Refresh(bool rebuildList)
    {
        _summary = StatsCalculator.Build(
            _sessions,
            _range,
            _settings.DayBoundary,
            _typeCatalog,
            _hiddenTypes);

        RangeText = Format.Range(_range);
        HoursText = Loc.S.Hours(Format.HoursNumber(_summary.TotalSeconds));
        CountText = Loc.S.Pomodoros(_summary.Count);
        MedianText = _summary.MedianDaySeconds is long median
            ? Loc.S.Median(Format.HoursNumber(median))
            : string.Empty;
        IsEmpty = _summary.IsEmpty;

        BuildLegend(_summary);
        BuildChart();

        if (rebuildList)
        {
            List.Load([.. VisibleSessions()], _typeCatalog);
        }
    }

    private IEnumerable<WorkSession> VisibleSessions() => _sessions
        .Where(session => !_hiddenTypes.Contains(WorkTypes.Canonical(session.Type, _typeCatalog)));

    private void BuildLegend(StatsSummary summary)
    {
        Legend.Clear();

        for (int index = 0; index < summary.AllTypes.Count; index++)
        {
            TypeTotal total = summary.AllTypes[index];

            Legend.Add(new LegendItemViewModel
            {
                Type = total.Type,
                Display = Loc.S.TypeDisplay(total.Type),
                Fill = TypePalette.For(total.Type, index),
                IsShown = !_hiddenTypes.Contains(total.Type),
                Hours = Loc.S.Hours(Format.HoursNumber(total.Seconds))
            });
        }
    }

    private void BuildChart()
    {
        Columns.Clear();
        GridLines.Clear();

        if (_summary is not StatsSummary summary)
        {
            return;
        }

        Dictionary<string, int> colorIndex = new(WorkTypes.Comparer);
        for (int index = 0; index < summary.AllTypes.Count; index++)
        {
            colorIndex[summary.AllTypes[index].Type] = index;
        }

        ChartScale scale = ChartLayout.Scale(summary.MaxDaySeconds);
        double columnWidth = ChartLayout.ColumnWidth(_plotWidth, summary.Days.Count);

        foreach (DayTotal day in summary.Days)
        {
            Columns.Add(new ChartColumnViewModel
            {
                Label = Format.DayNumber(day.Day),
                IsWeekend = day.IsWeekend,
                Tooltip = day.Seconds == 0L
                    ? Loc.S.DayEmptyTooltip(Format.DayAndMonth(day.Day))
                    : Format.DayAndMonth(day.Day) + " — " + Loc.S.Hours(Format.HoursNumber(day.Seconds)),
                Width = columnWidth,
                PlotHeight = _plotHeight,
                Segments = BuildSegments(day, scale, colorIndex)
            });
        }

        foreach (double hours in ChartLayout.GridHours(scale))
        {
            GridLines.Add(new GridLineViewModel
            {
                Top = ChartLayout.GridLineTop(hours, scale, _plotHeight) - LabelHalfHeight,
                LineWidth = _plotWidth,
                Label = Format.AxisHours(hours, scale.StepHours),
                Opacity = hours == 0.0 ? 0.9 : 0.45
            });
        }
    }

    private List<ChartSegmentViewModel> BuildSegments(
        DayTotal day,
        ChartScale scale,
        IReadOnlyDictionary<string, int> colorIndex)
    {
        List<ChartSegmentViewModel> segments = [];

        foreach (TypeTotal total in day.ByType)
        {
            segments.Add(new ChartSegmentViewModel
            {
                Fill = TypePalette.For(total.Type, colorIndex.GetValueOrDefault(total.Type)),
                Height = ChartLayout.SegmentHeight(total.Seconds, scale, _plotHeight),
                Tooltip = Loc.S.TypeDisplay(total.Type) + " — " + Loc.S.Hours(Format.HoursNumber(total.Seconds))
            });
        }

        return segments;
    }

    // Список не перестраиваем: правка пришла из него самого, и строка под курсором
    // уехала бы прямо во время редактирования.
    private void OnRowChanged(object? sender, SessionRowViewModel row)
    {
        int index = _sessions.FindIndex(session => session.Id == row.Id);

        if (index >= 0)
        {
            _sessions[index] = _sessions[index] with { DurationSec = row.DurationSec, Type = row.Type };
        }

        Refresh(rebuildList: false);
    }

    private void OnRowDeleted(object? sender, SessionRowViewModel row)
    {
        _store.Delete(row.Id);
        Reload();
    }
}
