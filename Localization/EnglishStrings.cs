using System.Globalization;
using Pomidoro.Domain;

namespace Pomidoro.Localization;

internal sealed class EnglishStrings : Strings
{
    private static readonly CultureInfo Culture = CultureInfo.InvariantCulture;

    public override AppLanguage Language => AppLanguage.English;

    public override string AppTitle => "Pomodoro tracker";

    public override string SettingsName => "Settings";

    public override string SettingsTip => "Timer settings";

    public override string StatsButton => "Statistics";

    public override string StatsCaption => "STATISTICS";

    public override string BackToTimer => "Back to the timer";

    public override string TodayCaption => "TODAY";

    public override string StartButton => "Start";

    public override string StopButton => "Stop";

    public override string PauseButton => "Pause";

    public override string ResumeButton => "Resume";

    public override string PhaseIdle => "READY TO WORK";

    public override string PhaseWork => "WORKING";

    public override string PhaseRest => "REST";

    public override string PausedSuffix => " · PAUSED";

    public override string HintIdle => "Work first, rest after";

    public override string HintPaused => "The timer is paused — this time will not be counted";

    public override string HintWorking => "Rest begins after the chime";

    public override string HintResting => "After the chime the timer can be started again";

    public override string ColumnDate => "DATE";

    public override string ColumnEnd => "FINISHED";

    public override string ColumnMinutes => "MINUTES";

    public override string ColumnType => "TYPE";

    public override string MinutesTip => "How long it lasted. Can be corrected";

    public override string TypeTip => "What you worked on. The list is edited under the gear";

    public override string DeleteRowName => "Delete the entry";

    public override string DeleteRowTip => "Delete the entry. No confirmation and no undo";

    public override string TodayEmpty => "Nothing worked today yet";

    public override string PeriodEmpty => "No pomodoros in this period";

    public override string PeriodName => "Statistics period";

    public override string PeriodTip => "Which period to show";

    public override string ChartEmpty => "Nothing was worked in this period";

    public override string LegendTip => "Click — keep only this type. Shift+click — add or remove";

    public override string NoType => "No type";

    public override string DurationCaption => "DURATION";

    public override string WorkMinutesCaption => "WORK, MIN";

    public override string WorkMinutesName => "Work duration in minutes";

    public override string RestMinutesCaption => "REST, MIN";

    public override string RestMinutesName => "Rest duration in minutes";

    public override string AppliesNextRun => "Applies on the next start";

    public override string DayBoundaryCaption => "DAY BOUNDARY";

    public override string HourCaption => "HOUR";

    public override string DayBoundaryName => "The hour at which a new day starts";

    public override string DayBoundaryTip => "When the day switches over to the next one";

    public override string TypesCaption => "TYPES";

    public override string TypesEmpty => "The list is empty";

    public override string RemoveTypeName => "Remove the type from the list";

    public override string RemoveTypeTip => "Remove from the list. In saved entries the type stays";

    public override string NewTypeNameLabel => "Name of the new type";

    public override string NewTypeTip => "New type";

    public override string AddButton => "Add";

    public override string SqlConsole => "Developer SQL";

    public override string SqlConsoleTip => "Arbitrary queries to the database, without checks";

    public override string QueryName => "Query text";

    public override string RunButton => "Run";

    public override string RunTip => "F5 or Ctrl+Enter. If there is a selection, only it runs";

    public override string ClearButton => "Clear";

    public override string SqlWarning =>
        "Queries go to the live connection of the application without checks. DROP, DELETE and the rest run as written.";

    public override string ResultName => "Query result";

    public override string TrayOpen => "Open";

    public override string TrayExit => "Exit";

    public override string SqlEmpty => "(empty)";

    public override string SqlRefreshFailedStatus => "the main window did not reload the database";

    public override string SqlRefreshFailedText => "After the query the main window could not refresh:";

    public override string Hours(string number) => number + " hours";

    public override string Pomodoros(int count) => count switch
    {
        0 => "no pomodoros",
        1 => "1 pomodoro",
        _ => string.Create(Culture, $"{count} pomodoros")
    };

    public override string Median(string hours) => "median " + hours + " h per day";

    public override string DayBoundaryHint(int hour) => hour == 0
        ? "The day ends at midnight"
        : string.Create(
            Culture,
            $"The day ends at {hour:D2}:00 — everything closed before that hour counts as yesterday");

    public override string PeriodTitle(StatsPeriod period) => period switch
    {
        StatsPeriod.CurrentWeek => "Current week",
        StatsPeriod.PreviousWeek => "Previous week",
        StatsPeriod.CurrentMonth => "Current month",
        _ => "Previous month"
    };

    public override string DayEmptyTooltip(string day) => day + ", empty";

    public override string SqlStatusError(long elapsedMs) =>
        string.Create(Culture, $"error · {elapsedMs} ms");

    public override string SqlStatusChanged(int changedRows, long elapsedMs) =>
        string.Create(Culture, $"done · rows changed: {changedRows} · {elapsedMs} ms");

    public override string SqlStatusRows(int rows, int changedRows, long elapsedMs)
    {
        string changed = changedRows > 0
            ? string.Create(Culture, $" · changed: {changedRows}")
            : string.Empty;

        return string.Create(Culture, $"rows: {rows}{changed} · {elapsedMs} ms");
    }

    public override string SqlDone(int changedRows) =>
        string.Create(Culture, $"Query executed. Rows changed: {changedRows}.");

    public override string SqlResultHeader(int index, int total) =>
        string.Create(Culture, $"-- result {index} of {total}");

    public override string SqlTruncated(int shown) =>
        string.Create(Culture, $"-- first {shown} rows shown, the rest discarded");

    public override string DatabaseOpenFailed(string path, string message) =>
        "Could not open the database:" + Environment.NewLine + path +
        Environment.NewLine + Environment.NewLine + message;

    public override string UnexpectedError(string exceptionType, string message) =>
        "Unexpected error, the application will close:" + Environment.NewLine + Environment.NewLine +
        exceptionType + ": " + message;
}
