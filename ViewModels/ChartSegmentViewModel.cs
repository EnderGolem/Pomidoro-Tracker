using System.Windows.Media;

namespace Pomidoro.ViewModels;

// Кусок столбика — один тип внутри одного дня.
internal sealed class ChartSegmentViewModel
{
    public required Brush Fill { get; init; }

    public required double Height { get; init; }

    public required string Tooltip { get; init; }
}
