using System.Text;
using Pomidoro.Localization;
using Pomidoro.Storage;

namespace Pomidoro.Formatting;

// Prints a query result as a monospace table: columns are aligned to
// the longest value, multiple results are separated by headers.
internal static class SqlResultText
{
    public static string Status(SqlQueryResult result)
    {
        if (result.Failed)
        {
            return Loc.S.SqlStatusError(result.ElapsedMs);
        }

        if (result.Sets.Count == 0)
        {
            return Loc.S.SqlStatusChanged(result.ChangedRows, result.ElapsedMs);
        }

        return Loc.S.SqlStatusRows(result.RowCount, result.ChangedRows, result.ElapsedMs);
    }

    public static string Render(SqlQueryResult result)
    {
        if (result.Error is string error)
        {
            return error;
        }

        if (result.Sets.Count == 0)
        {
            return Loc.S.SqlDone(result.ChangedRows);
        }

        StringBuilder text = new();

        for (int i = 0; i < result.Sets.Count; i++)
        {
            if (i > 0)
            {
                text.AppendLine();
                text.AppendLine();
            }

            if (result.Sets.Count > 1)
            {
                text.AppendLine(Loc.S.SqlResultHeader(i + 1, result.Sets.Count));
            }

            AppendSet(text, result.Sets[i]);
        }

        return text.ToString();
    }

    private static void AppendSet(StringBuilder text, SqlResultSet set)
    {
        int[] widths = new int[set.Columns.Count];

        for (int i = 0; i < set.Columns.Count; i++)
        {
            widths[i] = set.Columns[i].Length;
        }

        foreach (IReadOnlyList<string> row in set.Rows)
        {
            for (int i = 0; i < widths.Length && i < row.Count; i++)
            {
                widths[i] = Math.Max(widths[i], row[i].Length);
            }
        }

        text.AppendLine(string.Join(" | ", set.Columns.Select((name, i) => name.PadRight(widths[i]))));
        text.AppendLine(string.Join("-+-", widths.Select(width => new string('-', width))));

        foreach (IReadOnlyList<string> row in set.Rows)
        {
            text.AppendLine(string.Join(" | ", row.Select((cell, i) => cell.PadRight(widths[i]))));
        }

        if (set.Rows.Count == 0)
        {
            text.AppendLine(Loc.S.SqlEmpty);
        }

        if (set.Truncated)
        {
            text.AppendLine();
            text.AppendLine(Loc.S.SqlTruncated(set.Rows.Count));
        }
    }
}
