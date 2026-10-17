namespace Pomidoro.Storage;

internal sealed class SqlQueryResult
{
    public required IReadOnlyList<SqlResultSet> Sets { get; init; }

    public required int RecordsAffected { get; init; }

    public required long ElapsedMs { get; init; }

    public string? Error { get; init; }

    public bool Failed => Error is not null;

    public int RowCount => Sets.Sum(set => set.Rows.Count);

    // ExecuteReader returns -1 for SELECT: there's no point showing that number to the user.
    public int ChangedRows => Math.Max(RecordsAffected, 0);
}
