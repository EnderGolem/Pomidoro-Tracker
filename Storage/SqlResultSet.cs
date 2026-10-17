namespace Pomidoro.Storage;

internal sealed class SqlResultSet
{
    public required IReadOnlyList<string> Columns { get; init; }

    public required IReadOnlyList<IReadOnlyList<string>> Rows { get; init; }

    public required bool Truncated { get; init; }
}
