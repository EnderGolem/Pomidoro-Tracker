using System.Globalization;
using Pomidoro.Domain;

namespace Pomidoro.Localization;

internal sealed class RussianStrings : Strings
{
    private static readonly CultureInfo Culture = CultureInfo.InvariantCulture;

    public override AppLanguage Language => AppLanguage.Russian;

    public override string AppTitle => "Помидоро трекер";

    public override string SettingsName => "Настройки";

    public override string SettingsTip => "Настройки таймера";

    public override string StatsButton => "Статистика";

    public override string StatsCaption => "СТАТИСТИКА";

    public override string BackToTimer => "Назад к таймеру";

    public override string TodayCaption => "СЕГОДНЯ";

    public override string StartButton => "Пуск";

    public override string StopButton => "Стоп";

    public override string PauseButton => "Пауза";

    public override string ResumeButton => "Продолжить";

    public override string PhaseIdle => "ГОТОВ К РАБОТЕ";

    public override string PhaseWork => "ИДЁТ РАБОТА";

    public override string PhaseRest => "ОТДЫХ";

    public override string PausedSuffix => " · ПАУЗА";

    public override string HintIdle => "Сначала работа, потом отдых";

    public override string HintPaused => "Таймер на паузе — это время не пойдёт в зачёт";

    public override string HintWorking => "После сигнала начнётся отдых";

    public override string HintResting => "После сигнала таймер можно запустить снова";

    public override string ColumnDate => "ДАТА";

    public override string ColumnEnd => "ЗАКОНЧИЛ";

    public override string ColumnMinutes => "МИНУТ";

    public override string ColumnType => "ТИП";

    public override string MinutesTip => "Сколько минут длилось. Можно поправить";

    public override string TypeTip => "Над чем работал. Список настраивается под шестерёнкой";

    public override string DeleteRowName => "Удалить запись";

    public override string DeleteRowTip => "Удалить запись. Без подтверждения и без отмены";

    public override string TodayEmpty => "Сегодня ещё ничего не отработано";

    public override string PeriodEmpty => "Помидорок за этот период нет";

    public override string PeriodName => "Период статистики";

    public override string PeriodTip => "За какой период показывать";

    public override string ChartEmpty => "За этот период ничего не отработано";

    public override string LegendTip => "Клик — оставить только этот тип. Shift+клик — добавить или убрать";

    public override string NoType => "Без типа";

    public override string DurationCaption => "ДЛИТЕЛЬНОСТЬ";

    public override string WorkMinutesCaption => "РАБОТА, МИН";

    public override string WorkMinutesName => "Длительность работы в минутах";

    public override string RestMinutesCaption => "ОТДЫХ, МИН";

    public override string RestMinutesName => "Длительность отдыха в минутах";

    public override string AppliesNextRun => "Применится при следующем запуске";

    public override string DayBoundaryCaption => "ГРАНИЦА СУТОК";

    public override string HourCaption => "ЧАС";

    public override string DayBoundaryName => "Час, в который начинаются новые сутки";

    public override string DayBoundaryTip => "Во сколько сутки переключаются на следующие";

    public override string TypesCaption => "ТИПЫ";

    public override string TypesEmpty => "Список пуст";

    public override string RemoveTypeName => "Убрать тип из списка";

    public override string RemoveTypeTip => "Убрать из списка. В уже сохранённых записях тип останется";

    public override string NewTypeNameLabel => "Название нового типа";

    public override string NewTypeTip => "Новый тип";

    public override string AddButton => "Добавить";

    public override string SqlConsole => "SQL разработчика";

    public override string SqlConsoleTip => "Произвольные запросы к базе, без проверок";

    public override string QueryName => "Текст запроса";

    public override string RunButton => "Выполнить";

    public override string RunTip => "F5 или Ctrl+Enter. Если есть выделение, выполняется только оно";

    public override string ClearButton => "Очистить";

    public override string SqlWarning =>
        "Запросы идут в живое соединение приложения без проверок. DROP, DELETE и прочее выполнятся как есть.";

    public override string ResultName => "Результат запроса";

    public override string TrayOpen => "Открыть";

    public override string TrayExit => "Выход";

    public override string SqlEmpty => "(пусто)";

    public override string SqlRefreshFailedStatus => "главное окно не перечитало базу";

    public override string SqlRefreshFailedText => "После запроса главное окно не смогло обновиться:";

    // Дробное количество часов в русском всегда требует «часа»: 0.5 часа, 4.5 часа, 21.0 часа.
    public override string Hours(string number) => number + " часа";

    public override string Pomodoros(int count) => count == 0
        ? "ни одной помидорки"
        : string.Create(Culture, $"{count} {PomodoroWord(count)}");

    public override string Median(string hours) => "медиана " + hours + " ч в день";

    public override string DayBoundaryHint(int hour) => hour == 0
        ? "День заканчивается в полночь"
        : string.Create(
            Culture,
            $"День заканчивается в {hour:D2}:00 — всё, что закрыто до этого часа, идёт во вчерашний день");

    public override string PeriodTitle(StatsPeriod period) => period switch
    {
        StatsPeriod.CurrentWeek => "Текущая неделя",
        StatsPeriod.PreviousWeek => "Прошлая неделя",
        StatsPeriod.CurrentMonth => "Текущий месяц",
        _ => "Прошлый месяц"
    };

    public override string DayEmptyTooltip(string day) => day + ", пусто";

    public override string SqlStatusError(long elapsedMs) =>
        string.Create(Culture, $"ошибка · {elapsedMs} мс");

    public override string SqlStatusChanged(int changedRows, long elapsedMs) =>
        string.Create(Culture, $"выполнено · изменено строк: {changedRows} · {elapsedMs} мс");

    public override string SqlStatusRows(int rows, int changedRows, long elapsedMs)
    {
        string changed = changedRows > 0
            ? string.Create(Culture, $" · изменено: {changedRows}")
            : string.Empty;

        return string.Create(Culture, $"строк: {rows}{changed} · {elapsedMs} мс");
    }

    public override string SqlDone(int changedRows) =>
        string.Create(Culture, $"Запрос выполнен. Изменено строк: {changedRows}.");

    public override string SqlResultHeader(int index, int total) =>
        string.Create(Culture, $"-- результат {index} из {total}");

    public override string SqlTruncated(int shown) =>
        string.Create(Culture, $"-- показаны первые {shown} строк, остальные отброшены");

    public override string DatabaseOpenFailed(string path, string message) =>
        "Не удалось открыть базу данных:" + Environment.NewLine + path +
        Environment.NewLine + Environment.NewLine + message;

    public override string UnexpectedError(string exceptionType, string message) =>
        "Непредвиденная ошибка, приложение закроется:" + Environment.NewLine + Environment.NewLine +
        exceptionType + ": " + message;

    // «1 помидорка», «3 помидорки», «5 помидорок», «11 помидорок».
    private static string PomodoroWord(int count)
    {
        if (count % 100 is >= 11 and <= 14)
        {
            return "помидорок";
        }

        return (count % 10) switch
        {
            1 => "помидорка",
            2 or 3 or 4 => "помидорки",
            _ => "помидорок"
        };
    }
}
