using System.Windows.Media;

namespace Pomidoro.ViewModels;

// Кнопка-фильтр под диаграммой: тип, его цвет, часы и включён ли он в показ.
internal sealed class LegendItemViewModel
{
    public required string Type { get; init; }

    public required string Display { get; init; }

    public required Brush Fill { get; init; }

    public required bool IsShown { get; init; }

    public required string Hours { get; init; }
}
