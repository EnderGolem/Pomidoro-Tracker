namespace Pomidoro.ViewModels;

// Горизонтальная линия сетки с подписью часов слева. Лежит под столбиками
// и мышь не ловит, поэтому их подсказки не перехватывает.
internal sealed class GridLineViewModel
{
    public required double Top { get; init; }

    public required double LineWidth { get; init; }

    public required string Label { get; init; }

    public required double Opacity { get; init; }
}
