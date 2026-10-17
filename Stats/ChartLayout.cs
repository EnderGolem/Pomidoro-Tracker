namespace Pomidoro.Stats;

// Chart geometry in pixels. Kept apart from the markup because otherwise the bar width
// and the grid step could be neither read nor tested.
internal static class ChartLayout
{
    // Left strip for the hour labels. The same number is the bars' offset in the markup.
    public const double AxisWidth = 36.0;

    public const double MinColumnWidth = 7.0;
    public const double MaxColumnWidth = 46.0;
    public const double ColumnGap = 3.0;

    // So that a very short session doesn't degenerate into an invisible sliver.
    public const double MinSegmentHeight = 2.0;

    public const double MinPlotHeight = 60.0;

    // How much of the panel isn't taken by bars: paddings plus the day label under the axis.
    public const double ChartChrome = 46.0;

    private const int MaxGridLines = 4;

    private static readonly double[] GridSteps = [0.25, 0.5, 1.0, 2.0, 3.0, 4.0, 6.0, 8.0, 12.0, 24.0];

    public static double ColumnWidth(double plotWidth, int days) => days <= 0
        ? MinColumnWidth
        : Math.Clamp(plotWidth / days - ColumnGap, MinColumnWidth, MaxColumnWidth);

    public static double PlotWidth(double hostWidth) => Math.Max(hostWidth - AxisWidth, 0.0);

    public static double PlotHeight(double hostHeight) => Math.Max(hostHeight - ChartChrome, MinPlotHeight);

    // The axis ceiling is rounded to a round value, so the top grid line is labeled
    // with a whole number of hours and a bar can be read against it without hovering.
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
