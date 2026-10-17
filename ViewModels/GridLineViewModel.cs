namespace Pomidoro.ViewModels;

// Horizontal grid line with the hour label on the left. It lies under the bars
// and isn't hit-testable, so it doesn't intercept their tooltips.
internal sealed class GridLineViewModel
{
    public required double Top { get; init; }

    public required double LineWidth { get; init; }

    public required string Label { get; init; }

    public required double Opacity { get; init; }
}
