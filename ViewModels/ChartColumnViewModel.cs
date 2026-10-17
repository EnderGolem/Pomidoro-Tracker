namespace Pomidoro.ViewModels;

// One day's bar: segments from bottom to top and the label under the axis.
internal sealed class ChartColumnViewModel
{
    public required string Label { get; init; }

    public required bool IsWeekend { get; init; }

    public required string Tooltip { get; init; }

    public required double Width { get; init; }

    public required double PlotHeight { get; init; }

    public required IReadOnlyList<ChartSegmentViewModel> Segments { get; init; }
}
