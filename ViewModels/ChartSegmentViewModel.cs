using System.Windows.Media;

namespace Pomidoro.ViewModels;

// A bar segment: one type within one day.
internal sealed class ChartSegmentViewModel
{
    public required Brush Fill { get; init; }

    public required double Height { get; init; }

    public required string Tooltip { get; init; }
}
