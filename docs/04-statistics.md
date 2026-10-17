# Статистика

Папка `Stats/` отвечает на два вопроса: **что показывать** (`StatsCalculator`)
и **какого размера это рисовать** (`ChartLayout`). Оба класса — чистые функции:
на вход данные, на выход данные, ни базы, ни экрана.

## Типы результата

```csharp
internal sealed record TypeTotal(string Type, string Display, long Seconds);

internal sealed record DayTotal(DateOnly Day, long Seconds, IReadOnlyList<TypeTotal> ByType)
{
    public bool IsWeekend => Day.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday;
}

internal sealed record StatsSummary(
    long TotalSeconds,
    int Count,
    long? MedianDaySeconds,
    IReadOnlyList<DayTotal> Days,
    IReadOnlyList<TypeTotal> AllTypes)
{
    public long MaxDaySeconds => Days.Count == 0 ? 0L : Days.Max(day => day.Seconds);
    public bool IsEmpty => MaxDaySeconds == 0L;
}
```

`Type` — канонический ключ (пустая строка = тип не проставлен), `Display` — то,
что видит человек («Без типа» вместо пустоты). Хранить оба поля дешевле, чем
подставлять подпись в пяти местах и однажды забыть.

## `StatsCalculator.Build` по шагам

```csharp
public static StatsSummary Build(
    IReadOnlyList<WorkSession> sessions,
    DayRange range,
    DayBoundary boundary,
    IReadOnlyList<string> typeCatalog,
    IReadOnlySet<string> hiddenTypes)
```

**Шаг 1. Приведение типов.** Каждая запись получает канонический тип:

```csharp
List<TypedSession> typed = sessions
    .Select(session => new TypedSession(session, WorkTypes.Canonical(session.Type, typeCatalog)))
    .ToList();
```

С этого момента «Работа» и «работа» — один и тот же ключ. Почему это важно,
расписано в [02-domain.md](02-domain.md#приведение-к-написанию-из-справочника).

**Шаг 2. Типы для легенды — до фильтра.**

```csharp
IReadOnlyList<TypeTotal> allTypes = BuildTypeTotals(typed, typeCatalog);
```

Легенда показывает **все** типы периода, включая выключенные: иначе выключенный тип
исчез бы с экрана и включить его обратно было бы нечем.

**Шаг 3. Фильтр.** Из дальнейшего счёта уходят записи скрытых типов:

```csharp
List<TypedSession> visible = typed.Where(item => !hiddenTypes.Contains(item.Type)).ToList();
```

`hiddenTypes` приходит с компаратором без учёта регистра (`WorkTypes.NewSet()`),
поэтому выключение типа гасит все его написания разом.

**Шаг 4. Дни.** `BuildDays` раскладывает видимые записи по логическим дням
и перебирает **все** дни отрезка, а не только те, где была работа:

```csharp
ILookup<DateOnly, TypedSession> byDay = visible.ToLookup(item => boundary.DayOf(item.Session.EndLocal));

foreach (DateOnly day in range.EnumerateDays())
{
    List<TypeTotal> byType = byDay[day]
        .GroupBy(item => item.Type, WorkTypes.Comparer)
        .Select(group => new TypeTotal(group.Key, WorkTypes.Display(group.Key), group.Sum(...)))
        .OrderByDescending(total => total.Seconds)
        .ToList();

    days.Add(new DayTotal(day, byType.Sum(total => total.Seconds), byType));
}
```

Пустой день даёт `DayTotal` с нулём и пустым списком — на диаграмме он занимает
своё место на оси. Сегменты внутри дня сортируются от самого длинного к короткому,
чтобы крупные куски были снизу и столбик не рябил.

**Шаг 5. Итоги и медиана.**

```csharp
return new StatsSummary(
    visible.Sum(item => (long)item.Session.DurationSec),
    visible.Count,
    MedianOfWorkedDays(days),
    days,
    allTypes);
```

Приведение к `long` в сумме не случайно: `DurationSec` — `int`, и за несколько лет
сумма секунд вышла бы за его границы.

## Порядок типов и цвета

```csharp
List<string> ordered =
[
    .. typeCatalog.Where(seconds.ContainsKey),
    .. seconds.Keys
        .Where(type => type.Length > 0 && !typeCatalog.Contains(type, WorkTypes.Comparer))
        .Order(WorkTypes.Comparer)
];

if (seconds.ContainsKey(string.Empty))
{
    ordered.Add(string.Empty);
}
```

Порядок такой:

1. типы из справочника — в порядке справочника (то есть добавления);
2. типы, которых в справочнике уже нет, — по алфавиту;
3. «без типа» — всегда последним.

Порядок важен не только для вида: **цвет закрепляется за номером в этом списке**.

```csharp
public static Brush For(string canonicalType, int index) => canonicalType.Length == 0
    ? NoTypeBrush
    : Palette[index % Palette.Length];
```

Палитра — восемь цветов, «без типа» всегда серый и в палитре не участвует.
Если бы порядок был по сумме часов, цвет типа прыгал бы при каждом переключении
периода; если бы по алфавиту — перекрашивался бы при добавлении нового типа.
Порядок справочника устойчив к обоим случаям.

Кисти создаются один раз и замораживаются (`Freeze()`): замороженную кисть WPF
не копирует на каждую привязку и может использовать из любого потока.

## Медиана

```csharp
private static long? MedianOfWorkedDays(IReadOnlyList<DayTotal> days)
{
    List<long> worked = days.Where(day => day.Seconds > 0L).Select(day => day.Seconds).Order().ToList();

    if (worked.Count == 0)
    {
        return null;
    }

    int middle = worked.Count / 2;

    return worked.Count % 2 == 1
        ? worked[middle]
        : (worked[middle - 1] + worked[middle]) / 2L;
}
```

Два решения:

- **Считаются только дни, в которые была работа.** В месяце с шестью рабочими днями
  из тридцати медиана по всем дням всегда равна нулю и не говорит ничего.
  Вопрос спорный — но «сколько я обычно работаю в рабочий день» полезнее,
  чем «сколько я работаю в среднем по календарю».
- **Возвращается `long?`, а не ноль.** Ноль — законное значение: длительность записи
  можно поправить на 0 минут. Поэтому «данных нет» и «вышло ноль» различаются типом,
  а не значением. На экране `null` означает, что подписи про медиану просто нет.

При чётном количестве дней берётся среднее двух средних, целочисленным делением —
разница в полсекунды на экране всё равно не видна.

## Геометрия диаграммы

`ChartLayout` переводит секунды и доступное место в пиксели. Константы:

| Константа | Значение | Что это |
|---|---|---|
| `AxisWidth` | 36 | полоса слева под подписи часов |
| `ChartChrome` | 46 | что занято не столбиками: отступы и подпись дня |
| `MinPlotHeight` | 60 | ниже этого поле под столбики не сжимается |
| `MinColumnWidth` | 7 | уже — в столбик не попасть мышью |
| `MaxColumnWidth` | 46 | шире — столбики превращаются в ломти |
| `ColumnGap` | 3 | зазор между столбиками |
| `MinSegmentHeight` | 2 | чтобы короткая сессия не стала невидимой |
| `MaxGridLines` | 4 | сколько шагов сетки допустимо |

### Ширина столбика

```csharp
public static double ColumnWidth(double plotWidth, int days) => days <= 0
    ? MinColumnWidth
    : Math.Clamp(plotWidth / days - ColumnGap, MinColumnWidth, MaxColumnWidth);
```

Доступное место делится на количество дней. Если дней много (месяц в узком окне),
ширина упирается в минимум, суммарная ширина превышает панель и появляется
горизонтальная прокрутка — так задумано.

### Шаг сетки и потолок оси

```csharp
private static readonly double[] GridSteps = [0.25, 0.5, 1.0, 2.0, 3.0, 4.0, 6.0, 8.0, 12.0, 24.0];

public static ChartScale Scale(long maxDaySeconds)
{
    if (maxDaySeconds <= 0L) { return ChartScale.Empty; }

    double maxHours = maxDaySeconds / 3600.0;
    double step = GridSteps[^1];

    foreach (double candidate in GridSteps)
    {
        if (maxHours / candidate <= MaxGridLines) { step = candidate; break; }
    }

    return new ChartScale(Math.Max(Math.Ceiling(maxHours / step) * step, step) * 3600.0, step);
}
```

Берётся первый шаг из списка, при котором линий выходит не больше четырёх,
а потолок округляется вверх до целого числа шагов. Благодаря этому верхняя линия
всегда подписана круглым числом часов, и высоту столбика можно прочитать по сетке,
не наводя мышь.

Примеры:

| Самый длинный день | Шаг | Потолок | Линии |
|---|---|---|---|
| 30 минут | 0.25 ч | 0.5 ч | 0 · 0.25 · 0.5 |
| 3 часа | 1 ч | 3 ч | 0 · 1 · 2 · 3 |
| 5 ч 17 мин | 2 ч | 6 ч | 0 · 2 · 4 · 6 |
| 23 часа | 6 ч | 24 ч | 0 · 6 · 12 · 18 · 24 |

Пустой период даёт `ChartScale.Empty`, и сетка не рисуется вовсе.

### Высоты

```csharp
public static double SegmentHeight(long seconds, ChartScale scale, double plotHeight) => scale.IsEmpty
    ? 0.0
    : Math.Max(MinSegmentHeight, seconds / scale.MaxSeconds * plotHeight);

public static double GridLineTop(double hours, ChartScale scale, double plotHeight) => scale.IsEmpty
    ? plotHeight
    : plotHeight - hours / scale.MaxHours * plotHeight;
```

Высота сегмента — его доля от потолка оси. Нижняя линия сетки (ноль часов) оказывается
внизу поля, верхняя — наверху: в WPF начало координат сверху, поэтому высота вычитается.

Минимальная высота сегмента в 2 пикселя — приближение: если в одном дне окажется
два десятка крошечных сессий разных типов, суммарная высота столбика слегка превысит
расчётную. На практике типов единицы, и это незаметно.

### Размер панели

```csharp
public static double PlotWidth(double hostWidth) => Math.Max(hostWidth - AxisWidth, 0.0);
public static double PlotHeight(double hostHeight) => Math.Max(hostHeight - ChartChrome, MinPlotHeight);
```

Размер приходит из разметки — это единственная часть диаграммы, которую нельзя
посчитать заранее. Как именно он туда попадает, описано в
[05-ui.md](05-ui.md#размер-панели-диаграммы).

## Что считается при каких действиях

Пересчёт разделён на три уровня, чтобы не делать лишней работы:

| Действие | Запрос к базе | Пересчёт статистики | Пересборка списка строк |
|---|---|---|---|
| Открыли экран, сменили период | да | да | да |
| Клик по плашке легенды | нет | да | да |
| Правка минут или типа в строке | нет | да | **нет** |
| Изменение размера окна | нет | нет | нет |

Последняя строка — важная: при перетаскивании рамки окна событие размера приходит
десятки раз в секунду, и раньше каждое из них тянуло запрос к базе и пересборку
легенды. Сейчас меняется только геометрия (`SetPlotSize` → `BuildChart`).

Предпоследняя тоже: список не перестраивается после правки, иначе строка уехала бы
из-под курсора прямо во время редактирования.
