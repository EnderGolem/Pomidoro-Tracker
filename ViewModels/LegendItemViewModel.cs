using System.Windows.Media;

namespace Pomidoro.ViewModels;

// Filter button under the chart: the type, its color, hours and whether it's shown.
internal sealed class LegendItemViewModel
{
    public required string Type { get; init; }

    public required string Display { get; init; }

    public required Brush Fill { get; init; }

    public required bool IsShown { get; init; }

    public required string Hours { get; init; }
}
