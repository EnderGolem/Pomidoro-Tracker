namespace Pomidoro.Stats;

internal readonly record struct ChartScale(double MaxSeconds, double StepHours)
{
    public static ChartScale Empty => new(0.0, 0.0);

    public bool IsEmpty => MaxSeconds <= 0.0;

    public double MaxHours => MaxSeconds / 3600.0;
}
