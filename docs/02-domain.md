# Домен

Папка `Domain/` — правила предметной области. Ни базы, ни WPF, ни файлов, ни времени
из воздуха: всё приходит в параметрах. Поэтому всё отсюда покрыто тестами без
единой настоящей секунды ожидания.

## `WorkSession` — запись истории

```csharp
internal sealed record WorkSession
{
    public long Id { get; init; }
    public required DateTime EndUtc { get; init; }
    public required int DurationSec { get; init; }
    public string Type { get; init; } = string.Empty;

    public DateTime EndLocal => EndUtc.ToLocalTime();
    public TimeSpan Duration => TimeSpan.FromSeconds(DurationSec);
}
```

Это **единственное**, что попадает в историю: перерывы и паузы не сохраняются.
Записывается момент окончания, а не начала — именно он отвечает на вопрос
«в какой день засчитать».

Три решения, которые стоит понимать:

- **`record`, а не `class`.** Нужен `with`: когда в списке поправили минуты, модель
  представления подменяет запись в своём кэше копией с новым значением,
  а не мутирует её. Неизменяемость тут дешевле аккуратности.
- **`Id = 0` у ещё не сохранённой записи.** Идентификатор присваивает база.
- **`Type` — строка, а не ссылка на справочник.** Убранный из справочника тип
  обязан остаться в истории. Подробнее — в разделе [Типы работы](#типы-работы).

Колонки `LocalDate` в этом типе нет, хотя в базе она есть: это справочное поле,
которое заполняет хранилище. См. [03-storage.md](03-storage.md#схема).

## `PomodoroTimer` — машина состояний

Самая важная часть домена. Таймер не умеет ни тикать сам, ни сохранять, ни звенеть —
он только считает время и отвечает, что сейчас происходит.

### Состояния

| Фаза | Что значит |
|---|---|
| `Idle` | таймер в покое, ждёт «Пуск» |
| `Work` | идёт рабочий промежуток |
| `Rest` | идёт отдых |

Плюс признак «на паузе», который относится к `Work` и `Rest`. В покое паузы не бывает.

### Переходы

| Было | Вызвали | Стало | Побочный результат |
|---|---|---|---|
| `Idle` | `Start(work, rest)` | `Work` | — |
| `Work`/`Rest` | `Start(...)` | не меняется | повторный пуск игнорируется |
| `Work`/`Rest` идёт | `Pause()` | та же фаза, на паузе | накопленное время замораживается |
| на паузе | `Resume()` | та же фаза, идёт | — |
| `Work` | `Advance()` при `Remaining == 0` | `Rest` | `TimerStep(WorkFinished, запись)` |
| `Rest` | `Advance()` при `Remaining == 0` | `Idle` | `TimerStep(RestFinished, null)` |
| `Work` | `Stop()` | `Idle` | запись, если отработано ≥ 1 секунды |
| `Rest` | `Stop()` | `Idle` | ничего не записывается |
| любое | `Advance()` до конца фазы или на паузе | не меняется | `TimerStep.Nothing` |

### Как считается время

```csharp
public TimeSpan Elapsed => _runningSince is long since
    ? _elapsedBeforePause + _time.GetElapsedTime(since)
    : _elapsedBeforePause;
```

Два поля: `_elapsedBeforePause` — сколько уже накопили, `_runningSince` — метка времени
начала текущего отрезка (или `null`, если стоим на паузе). `Pause` переносит текущий
отрезок в накопленное и обнуляет метку, `Resume` ставит метку заново.

Отсюда два следствия:

- **Время на паузе нигде не копится.** Не «вычитается потом», а просто не идёт.
- **Количество тиков ни на что не влияет.** Если интерфейс подвис на полсекунды,
  таймер не отстанет: он смотрит на метки времени, а не считает вызовы.

`GetElapsedTime` использует монотонный счётчик (`GetTimestamp`), а не настенные часы,
поэтому перевод системного времени или поправка от NTP таймер не ломают.

### Почему записывается плановая длительность

```csharp
// Длительность берём плановую, а не измеренную: тик мог прийти на десятки
// миллисекунд позже нуля, и в истории это выглядело бы как 25:00,08.
WorkSession completed = NewSession(PhaseLength);
```

Тик приходит раз в 100 мс, так что момент «ровно ноль» мы всегда проскакиваем.
Если записывать измеренное, в базе копились бы 1500,07 с вместо 1500.
А вот при досрочной остановке пишется именно измеренное — там плановая длительность
смысла не имеет.

Порог `MinRecordedSeconds = 1` отсекает случай «нажал Пуск и сразу Стоп».

### Откуда берётся время

```csharp
public PomodoroTimer(TimeProvider time)
```

`TimeProvider` — стандартный способ в .NET 8+ не зависеть от часов напрямую.
В приложении передаётся `TimeProvider.System`, в тестах — `FakeTime`, у которого
время двигается вручную:

```csharp
_timer.Start(Work, Rest);
_time.Advance(TimeSpan.FromMinutes(10));
_timer.Pause();
_time.Advance(TimeSpan.FromHours(3));   // на паузе — не считается
Assert.Equal(TimeSpan.FromMinutes(10), _timer.Elapsed);
```

Полный цикл с паузами проверяется мгновенно. См. [06-testing.md](06-testing.md).

### `TimerStep` и `TimerEvent`

`Advance()` возвращает не `void` и не `bool`, а описание того, что случилось:

```csharp
internal readonly record struct TimerStep(TimerEvent Event, WorkSession? Completed)
{
    public static TimerStep Nothing => new(TimerEvent.Nothing, null);
}
```

`TimerEvent` — `Nothing`, `WorkFinished`, `RestFinished`. Такой возврат вместо событий
или `out`-параметров выбран сознательно: вызывающий код читается сверху вниз одним куском,
и тест может просто сравнить результат.

## `DayBoundary` — где кончаются сутки

```csharp
internal readonly record struct DayBoundary
{
    public const int MinHour = 0;
    public const int MaxHour = 23;
    public const int DefaultHour = 0;
}
```

Час задаётся в настройках и приводится в допустимый диапазон прямо в конструкторе:
`new DayBoundary(99).Hour == 23`. Проверять значение снаружи не нужно.

| Метод | Что делает |
|---|---|
| `DayOf(DateTime local)` | какому логическому дню принадлежит момент |
| `Today(TimeProvider time)` | какой логический день идёт сейчас |
| `StartUtc(DateOnly day)` | момент начала дня в UTC |
| `EndUtc(DateOnly day)` | момент конца дня в UTC (он же начало следующего) |

Вся арифметика сводится к одной строке:

```csharp
public DateOnly DayOf(DateTime local) => DateOnly.FromDateTime(local.AddHours(-Hour));
```

Сдвигаем момент назад на величину границы и берём календарную дату. При границе `4`
время `17 октября, 03:59` превращается в `16 октября, 23:59` — и засчитывается
в шестнадцатое. При границе `0` ничего не меняется.

`StartUtc`/`EndUtc` нужны хранилищу, чтобы превратить «день» в пару меток для `WHERE`:

```csharp
public DateTime StartUtc(DateOnly day) =>
    DateTime.SpecifyKind(day.ToDateTime(TimeOnly.MinValue).AddHours(Hour), DateTimeKind.Unspecified)
        .ToUniversalTime();
```

Дата + час границы считаются локальным временем и переводятся в UTC.

> **Край.** Если граница попадает в час, который при переходе на летнее время
> пропускается, `ToUniversalTime` выберет ближайший допустимый момент. В России
> перевода часов нет, и проверять этот случай в приложении незачем.

Тесты специально написаны так, чтобы не зависеть от часового пояса машины:
проверяется не конкретный UTC-момент, а то, что начало и конец дня снова
попадают в тот же день.

## `DayRange` — отрезок дней

```csharp
internal readonly record struct DayRange(DateOnly From, DateOnly To)
```

Нужен, чтобы не таскать по всему коду пару `(from, to)` и не путать их местами.
Умеет немного: `Days` (сколько дней, включая оба конца), `Contains`, `EnumerateDays`,
`IsSingleDay` и `SingleDay(day)` для «сегодня».

`EnumerateDays` возвращает **все** дни отрезка, включая пустые, — на этом держится
диаграмма: день без работы всё равно должен занимать своё место на оси.

## `StatsPeriods` — периоды статистики

Перечисление `StatsPeriod`: `CurrentWeek`, `PreviousWeek`, `CurrentMonth`, `PreviousMonth`.

```csharp
public static DayRange Resolve(StatsPeriod period, DateOnly today) => period switch
{
    StatsPeriod.CurrentWeek  => new DayRange(WeekStart(today), today),
    StatsPeriod.PreviousWeek => new DayRange(WeekStart(today).AddDays(-7), WeekStart(today).AddDays(-1)),
    StatsPeriod.CurrentMonth => new DayRange(MonthStart(today), today),
    StatsPeriod.PreviousMonth=> new DayRange(MonthStart(today).AddMonths(-1), MonthStart(today).AddDays(-1)),
    _ => DayRange.SingleDay(today)
};
```

Текущие периоды кончаются **сегодня**, а не в воскресенье и не 31-го числа: показывать
пустой хвост будущего незачем. Прошлые периоды берутся целиком.

Неделя начинается с понедельника:

```csharp
private static DateOnly WeekStart(DateOnly day) => day.AddDays(-(((int)day.DayOfWeek + 6) % 7));
```

`DayOfWeek` в .NET считает воскресенье нулём, поэтому `+6 % 7` сдвигает отсчёт.
Для воскресенья получается 6 — и неделя всё равно начинается с прошедшего понедельника.

`Title(period)` даёт подпись для выпадающего списка, `All` — все значения по порядку.
Порядок в `All` определяет порядок пунктов на экране.

## Типы работы

`WorkTypes` — маленький класс, на котором держится вся работа с типами.

```csharp
public const string NoTypeLabel = "Без типа";    // подпись в легенде
public const string NoTypeOption = "—";          // пункт выпадающего списка в строке
public static readonly StringComparer Comparer = StringComparer.OrdinalIgnoreCase;
```

### Пустой тип и «без типа» — разные вещи

Ключ группировки у записи без типа — **пустая строка**. Подпись «Без типа»
подставляется только при показе:

```csharp
public static string Display(string canonicalType) =>
    canonicalType.Length == 0 ? NoTypeLabel : canonicalType;
```

Поэтому тип, который пользователь назвал бы «Без типа», не сольётся с настоящими
пустыми. На всякий случай такое имя (как и прочерк) в справочник просто не пускают:
`IsReservedName`.

### Приведение к написанию из справочника

```csharp
public static string Canonical(string? storedType, IReadOnlyList<string> catalog)
```

Берёт то, что лежит в записи, и возвращает написание из справочника, если там есть
совпадение без учёта регистра. Если совпадения нет — возвращает как есть, обрезав пробелы.
Пустая строка и пробелы превращаются в пустой ключ.

Зачем это нужно:

- справочник **не различает регистр** (нельзя завести и «созвон», и «СОЗВОН»);
- в записях при этом может лежать что угодно — например, после того как тип убрали
  из справочника и добавили заново с другой буквы, или после правки через SQL-консоль;
- если группировать по сырой строке, «Работа» и «работа» дадут **две плашки в легенде,
  два цвета на диаграмме и два отдельных фильтра**, хотя в списке обе строки
  выглядят одинаково.

Приведение делается **только при показе**. Саму историю приложение не переписывает:
что записано, то и лежит. Проверить можно через SQL-консоль — там видно обе строки.

`Comparer` и `NewSet()` используются везде, где типы сравниваются или складываются
в множество: набор скрытых типов, словари цветов, группировки.

### Почему регистр сравнивается в C#, а не в SQL

Встроенный в SQLite `COLLATE NOCASE` сворачивает регистр **только для латиницы `A-Z`**.
Для кириллицы он бесполезен: «созвон» и «СОЗВОН» он считает разными словами.
Поэтому проверка на дубликат живёт в `SqliteSessionStore.AddTypeToCatalog`,
а в схеме осталось обычное `UNIQUE`.
