namespace Pomidoro.Stats;

// Геометрия диаграммы в пикселях. Отдельно от разметки, потому что иначе ширину
// столбика и шаг сетки нельзя ни прочитать, ни проверить тестом.
internal static class ChartLayout
{
    // Полоса слева под подписи часов. Та же цифра стоит отступом столбиков в разметке.
    public const double AxisWidth = 36.0;

    public const double MinColumnWidth = 7.0;
    public const double MaxColumnWidth = 46.0;
    public const double ColumnGap = 3.0;

    // Чтобы совсем короткая сессия не выродилась в невидимую полоску.
    public const double MinSegmentHeight = 2.0;

    public const double MinPlotHeight = 60.0;

    // Сколько в панели занято не столбиками: отступы плюс подпись дня под осью.
    public const double ChartChrome = 46.0;

    private const int MaxGridLines = 4;

    private static readonly double[] GridSteps = [0.25, 0.5, 1.0, 2.0, 3.0, 4.0, 6.0, 8.0, 12.0, 24.0];

    public static double ColumnWidth(double plotWidth, int days) => days <= 0
        ? MinColumnWidth
        : Math.Clamp(plotWidth / days - ColumnGap, MinColumnWidth, MaxColumnWidth);

    public static double PlotWidth(double hostWidth) => Math.Max(hostWidth - AxisWidth, 0.0);

    public static double PlotHeight(double hostHeight) => Math.Max(hostHeight - ChartChrome, MinPlotHeight);

    // Потолок оси округляем до круглого значения — тогда верхняя линия сетки подписана
    // целым числом часов и столбик читается по ней без наведения мышью.
    public static ChartScale Scale(long maxDaySeconds)
    {
        if (maxDaySeconds <= 0L)
        {
            return ChartScale.Empty;
        }

        double maxHours = maxDaySeconds / 3600.0;
        double step = GridSteps[^1];

        foreach (double candidate in GridSteps)
        {
            if (maxHours / candidate <= MaxGridLines)
            {
                step = candidate;
                break;
            }
        }

        return new ChartScale(Math.Max(Math.Ceiling(maxHours / step) * step, step) * 3600.0, step);
    }

    public static IReadOnlyList<double> GridHours(ChartScale scale)
    {
        if (scale.IsEmpty)
        {
            return [];
        }

        List<double> hours = [];

        for (double value = 0.0; value <= scale.MaxHours + 0.001; value += scale.StepHours)
        {
            hours.Add(value);
        }

        return hours;
    }

    public static double SegmentHeight(long seconds, ChartScale scale, double plotHeight) => scale.IsEmpty
        ? 0.0
        : Math.Max(MinSegmentHeight, seconds / scale.MaxSeconds * plotHeight);

    public static double GridLineTop(double hours, ChartScale scale, double plotHeight) => scale.IsEmpty
        ? plotHeight
        : plotHeight - hours / scale.MaxHours * plotHeight;
}
