# Statistics

The `Stats/` folder answers two questions: **what to show** (`StatsCalculator`)
and **how big to draw it** (`ChartLayout`). Both classes are pure functions:
data in, data out, no database and no screen.

## Result types

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

`Type` is the canonical key (an empty string means no type was set), `Display` is
what a person sees ("No type" instead of nothing). Storing both fields is cheaper than
substituting the label in five places and forgetting one some day.

## `StatsCalculator.Build` step by step

```csharp
public static StatsSummary Build(
    IReadOnlyList<WorkSession> sessions,
    DayRange range,
    DayBoundary boundary,
    IReadOnlyList<string> typeCatalog,
    IReadOnlySet<string> hiddenTypes)
```

**Step 1. Normalizing types.** Every record gets its canonical type:

```csharp
List<TypedSession> typed = sessions
    .Select(session => new TypedSession(session, WorkTypes.Canonical(session.Type, typeCatalog)))
    .ToList();
```

From this point on «Работа» and «работа» are one and the same key. Why that matters
is explained in [02-domain.md](02-domain.md#normalizing-to-the-catalog-spelling).

**Step 2. Legend types — before the filter.**

```csharp
IReadOnlyList<TypeTotal> allTypes = BuildTypeTotals(typed, typeCatalog);
```

The legend shows **all** types of the period, including the disabled ones: otherwise
a disabled type would vanish from the screen and there would be nothing to click
to bring it back.

**Step 3. Filter.** Records of hidden types drop out of further counting:

```csharp
List<TypedSession> visible = typed.Where(item => !hiddenTypes.Contains(item.Type)).ToList();
```

`hiddenTypes` comes with a case-insensitive comparer (`WorkTypes.NewSet()`),
so disabling a type hides all of its spellings at once.

**Step 4. Days.** `BuildDays` distributes the visible records over logical days
and walks **every** day of the range, not only the days with work:

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

An empty day yields a `DayTotal` with zero and an empty list — on the chart it keeps
its place on the axis. Segments within a day are sorted from longest to shortest,
so the big pieces sit at the bottom and the column does not look striped.

**Step 5. Totals and median.**

```csharp
return new StatsSummary(
    visible.Sum(item => (long)item.Session.DurationSec),
    visible.Count,
    MedianOfWorkedDays(days),
    days,
    allTypes);
```

The cast to `long` in the sum is not accidental: `DurationSec` is an `int`, and over
a few years the sum of seconds would overflow it.

## Type order and colours

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

The order is:

1. types from the catalog — in catalog order (that is, order of addition);
2. types no longer in the catalog — alphabetically;
3. the "no type" bucket — always last.

The order matters for more than looks: **a colour is bound to the position in this list**.

```csharp
public static Brush For(string canonicalType, int index) => canonicalType.Length == 0
    ? NoTypeBrush
    : Palette[index % Palette.Length];
```

The palette has eight colours; "no type" is always grey and does not take part in it.
If the order were by total hours, a type's colour would jump every time the period
changes; if alphabetical, it would repaint whenever a new type is added.
Catalog order is stable against both.

Brushes are created once and frozen (`Freeze()`): WPF does not copy a frozen brush
for every binding and can use it from any thread.

## Median

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

Two decisions:

- **Only days with work are counted.** In a month with six working days out of thirty,
  the median over all days is always zero and says nothing.
  It is debatable — but "how much do I usually work on a working day" is more useful
  than "how much do I work on average per calendar day".
- **It returns `long?`, not zero.** Zero is a legitimate value: a record's duration
  can be corrected to 0 minutes. So "no data" and "came out as zero" are told apart by type,
  not by value. On screen, `null` means the median caption simply is not shown.

With an even number of days, the mean of the two middle values is taken using integer
division — a half-second difference is invisible on screen anyway.

## Chart geometry

`ChartLayout` turns seconds and available space into pixels. Constants:

| Constant | Value | What it is |
|---|---|---|
| `AxisWidth` | 36 | strip on the left for the hour labels |
| `ChartChrome` | 46 | space not taken by columns: padding and the day label |
| `MinPlotHeight` | 60 | the plot area does not shrink below this |
| `MinColumnWidth` | 7 | any narrower and the column is hard to hit with the mouse |
| `MaxColumnWidth` | 46 | any wider and columns turn into slabs |
| `ColumnGap` | 3 | gap between columns |
| `MinSegmentHeight` | 2 | so a short session does not become invisible |
| `MaxGridLines` | 4 | how many grid steps are allowed |

### Column width

```csharp
public static double ColumnWidth(double plotWidth, int days) => days <= 0
    ? MinColumnWidth
    : Math.Clamp(plotWidth / days - ColumnGap, MinColumnWidth, MaxColumnWidth);
```

The available space is divided by the number of days. If there are many days (a month
in a narrow window), the width hits the minimum, the total width exceeds the panel and
horizontal scrolling appears — this is intended.

### Grid step and axis ceiling

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

The first step in the list that yields no more than four lines is taken, and the ceiling
is rounded up to a whole number of steps. Thanks to this the top line is always labelled
with a round number of hours, and a column's height can be read off the grid without
hovering the mouse.

Examples:

| Longest day | Step | Ceiling | Lines |
|---|---|---|---|
| 30 minutes | 0.25 h | 0.5 h | 0 · 0.25 · 0.5 |
| 3 hours | 1 h | 3 h | 0 · 1 · 2 · 3 |
| 5 h 17 min | 2 h | 6 h | 0 · 2 · 4 · 6 |
| 23 hours | 6 h | 24 h | 0 · 6 · 12 · 18 · 24 |

An empty period yields `ChartScale.Empty`, and no grid is drawn at all.

### Heights

```csharp
public static double SegmentHeight(long seconds, ChartScale scale, double plotHeight) => scale.IsEmpty
    ? 0.0
    : Math.Max(MinSegmentHeight, seconds / scale.MaxSeconds * plotHeight);

public static double GridLineTop(double hours, ChartScale scale, double plotHeight) => scale.IsEmpty
    ? plotHeight
    : plotHeight - hours / scale.MaxHours * plotHeight;
```

A segment's height is its share of the axis ceiling. The bottom grid line (zero hours)
ends up at the bottom of the plot and the top line at the top: in WPF the origin is
at the top, so the height is subtracted.

The 2-pixel minimum segment height is an approximation: if a single day holds a couple
of dozen tiny sessions of different types, the column's total height will slightly exceed
the computed one. In practice there are only a handful of types, and it is unnoticeable.

### Panel size

```csharp
public static double PlotWidth(double hostWidth) => Math.Max(hostWidth - AxisWidth, 0.0);
public static double PlotHeight(double hostHeight) => Math.Max(hostHeight - ChartChrome, MinPlotHeight);
```

The size comes from the markup — it is the only part of the chart that cannot be
computed in advance. How exactly it gets there is described in
[05-ui.md](05-ui.md#chart-panel-size).

## What gets recomputed on which action

Recomputation is split into three levels to avoid unnecessary work:

| Action | Database query | Statistics recomputed | Row list rebuilt |
|---|---|---|---|
| Screen opened, period changed | yes | yes | yes |
| Legend chip clicked | no | yes | yes |
| Minutes or type edited in a row | no | yes | **no** |
| Window resized | no | no | no |

The last row is important: while the window frame is being dragged, the size event fires
dozens of times per second, and each one used to trigger a database query and a legend
rebuild. Now only the geometry changes (`SetPlotSize` → `BuildChart`).

The second-to-last one too: the list is not rebuilt after an edit, otherwise the row
would slide out from under the cursor right in the middle of editing.
