namespace Pomidoro.Domain;

internal static class WorkTypes
{
    public const string NoTypeOption = "—";

    public static readonly StringComparer Comparer = StringComparer.OrdinalIgnoreCase;

    // The "no type" labels of both languages are reserved at once and for good. Otherwise a type
    // created in the English UI would collide with the synthetic bucket after
    // switching to Russian, and vice versa.
    public static readonly IReadOnlyList<string> ReservedNames =
    [
        "Без типа",
        "No type",
        NoTypeOption
    ];

    public static HashSet<string> NewSet() => new(Comparer);

    // A record stores the type as a string, while the catalog compares names case-insensitively.
    // Without normalizing to the catalog's spelling, "Work" and "work" split
    // into two separate entries in the chart, the legend and the totals.
    public static string Canonical(string? storedType, IReadOnlyList<string> catalog)
    {
        if (string.IsNullOrWhiteSpace(storedType))
        {
            return string.Empty;
        }

        string trimmed = storedType.Trim();

        foreach (string known in catalog)
        {
            if (Comparer.Equals(known, trimmed))
            {
                return known;
            }
        }

        return trimmed;
    }

    public static bool IsReservedName(string name) => ReservedNames.Contains(name, Comparer);
}
