using Pomidoro.Domain;

namespace Pomidoro.Localization;

// All UI text in one list. The class is abstract on purpose: a forgotten string
// is caught by the compiler, not by a user who sees an empty label.
internal abstract class Strings
{
    public abstract AppLanguage Language { get; }

    public abstract string AppTitle { get; }

    public abstract string SettingsName { get; }

    public abstract string SettingsTip { get; }

    public abstract string StatsButton { get; }

    public abstract string StatsCaption { get; }

    public abstract string BackToTimer { get; }

    public abstract string TodayCaption { get; }

    public abstract string StartButton { get; }

    public abstract string StopButton { get; }

    public abstract string PauseButton { get; }

    public abstract string ResumeButton { get; }

    public abstract string PhaseIdle { get; }

    public abstract string PhaseWork { get; }

    public abstract string PhaseRest { get; }

    public abstract string PausedSuffix { get; }

    public abstract string HintIdle { get; }

    public abstract string HintPaused { get; }

    public abstract string HintWorking { get; }

    public abstract string HintResting { get; }

    public abstract string ColumnDate { get; }

    public abstract string ColumnEnd { get; }

    public abstract string ColumnMinutes { get; }

    public abstract string ColumnType { get; }

    public abstract string MinutesTip { get; }

    public abstract string TypeTip { get; }

    public abstract string DeleteRowName { get; }

    public abstract string DeleteRowTip { get; }

    public abstract string TodayEmpty { get; }

    public abstract string PeriodEmpty { get; }

    public abstract string PeriodName { get; }

    public abstract string PeriodTip { get; }

    public abstract string ChartEmpty { get; }

    public abstract string LegendTip { get; }

    public abstract string NoType { get; }

    public abstract string DurationCaption { get; }

    public abstract string WorkMinutesCaption { get; }

    public abstract string WorkMinutesName { get; }

    public abstract string RestMinutesCaption { get; }

    public abstract string RestMinutesName { get; }

    public abstract string AppliesNextRun { get; }

    public abstract string DayBoundaryCaption { get; }

    public abstract string HourCaption { get; }

    public abstract string DayBoundaryName { get; }

    public abstract string DayBoundaryTip { get; }

    public abstract string TypesCaption { get; }

    public abstract string TypesEmpty { get; }

    public abstract string RemoveTypeName { get; }

    public abstract string RemoveTypeTip { get; }

    public abstract string NewTypeNameLabel { get; }

    public abstract string NewTypeTip { get; }

    public abstract string AddButton { get; }

    public abstract string SqlConsole { get; }

    public abstract string SqlConsoleTip { get; }

    public abstract string QueryName { get; }

    public abstract string RunButton { get; }

    public abstract string RunTip { get; }

    public abstract string ClearButton { get; }

    public abstract string SqlWarning { get; }

    public abstract string ResultName { get; }

    public abstract string TrayOpen { get; }

    public abstract string TrayExit { get; }

    public abstract string SqlEmpty { get; }

    public abstract string SqlRefreshFailedStatus { get; }

    public abstract string SqlRefreshFailedText { get; }

    // An empty type and a type the user might name "No type" are different things:
    // the former's grouping key is an empty string, and its label is substituted only here.
    public string TypeDisplay(string canonicalType) =>
        canonicalType.Length == 0 ? NoType : canonicalType;

    public abstract string Hours(string number);

    public abstract string Pomodoros(int count);

    public abstract string Median(string hours);

    public abstract string DayBoundaryHint(int hour);

    public abstract string PeriodTitle(StatsPeriod period);

    public abstract string DayEmptyTooltip(string day);

    public abstract string SqlStatusError(long elapsedMs);

    public abstract string SqlStatusChanged(int changedRows, long elapsedMs);

    public abstract string SqlStatusRows(int rows, int changedRows, long elapsedMs);

    public abstract string SqlDone(int changedRows);

    public abstract string SqlResultHeader(int index, int total);

    public abstract string SqlTruncated(int shown);

    public abstract string DatabaseOpenFailed(string path, string message);

    public abstract string UnexpectedError(string exceptionType, string message);
}
