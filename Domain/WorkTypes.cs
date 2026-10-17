namespace Pomidoro.Domain;

internal static class WorkTypes
{
    public const string NoTypeOption = "—";

    public static readonly StringComparer Comparer = StringComparer.OrdinalIgnoreCase;

    // Подписи «без типа» обоих языков закрыты сразу и навсегда. Иначе тип, заведённый
    // в английском интерфейсе, столкнулся бы с синтетической корзиной после
    // переключения на русский — и наоборот.
    public static readonly IReadOnlyList<string> ReservedNames =
    [
        "Без типа",
        "No type",
        NoTypeOption
    ];

    public static HashSet<string> NewSet() => new(Comparer);

    // В записи тип лежит строкой, а справочник сравнивает имена без учёта регистра.
    // Без приведения к написанию из справочника «Работа» и «работа» разъезжаются
    // на две разные строки в диаграмме, в легенде и в итогах.
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
