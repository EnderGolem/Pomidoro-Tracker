using Pomidoro.Domain;

namespace Pomidoro.Stats;

// All the math for the statistics screen: totals, breakdown by day and by type, median.
// The class knows nothing about the database or the UI, so tests cover it entirely.
internal static class StatsCalculator
{
    public static StatsSummary Build(
        IReadOnlyList<WorkSession> sessions,
        DayRange range,
        DayBoundary boundary,
        IReadOnlyList<string> typeCatalog,
        IReadOnlySet<string> hiddenTypes)
    {
        List<TypedSession> typed = sessions
            .Select(session => new TypedSession(session, WorkTypes.Canonical(session.Type, typeCatalog)))
            .ToList();

        // The legend shows all types of the period, even hidden ones, so it is built before the filter.
        IReadOnlyList<TypeTotal> allTypes = BuildTypeTotals(typed, typeCatalog);

        List<TypedSession> visible = typed
            .Where(item => !hiddenTypes.Contains(item.Type))
            .ToList();

        IReadOnlyList<DayTotal> days = BuildDays(visible, range, boundary);

        return new StatsSummary(
            visible.Sum(item => (long)item.Session.DurationSec),
            visible.Count,
            MedianOfWorkedDays(days),
            days,
            allTypes);
    }

    // The median counts only days that had work: otherwise in a month
    // with six working days out of thirty it would always be zero.
    private static long? MedianOfWorkedDays(IReadOnlyList<DayTotal> days)
    {
        List<long> worked = days
            .Where(day => day.Seconds > 0L)
            .Select(day => day.Seconds)
            .Order()
            .ToList();

        if (worked.Count == 0)
        {
            return null;
        }

        int middle = worked.Count / 2;

        return worked.Count % 2 == 1
            ? worked[middle]
            : (worked[middle - 1] + worked[middle]) / 2L;
    }

    // Type order: catalog order first, then forgotten types alphabetically,
    // "no type" always last. The color assigned to a type depends on this order.
    private static List<TypeTotal> BuildTypeTotals(
        IReadOnlyList<TypedSession> typed,
        IReadOnlyList<string> typeCatalog)
    {
        Dictionary<string, long> seconds = new(WorkTypes.Comparer);

        foreach (TypedSession item in typed)
        {
            seconds[item.Type] = seconds.GetValueOrDefault(item.Type) + item.Session.DurationSec;
        }

        List<string> ordered =
        [
            .. typeCatalog.Where(seconds.ContainsKey),
            .. seconds.Keys
                .Where(type => type.Length > 0 && !typeCatalog.Contains(type, WorkTypes.Comparer))
                .Order(WorkTypes.Comparer)
        ];

        if (seconds.ContainsKey(string.Empty))
        {
            ordered.Add(string.Empty);
        }

        return ordered
            .Select(type => new TypeTotal(type, seconds[type]))
            .ToList();
    }

    private static List<DayTotal> BuildDays(
        IReadOnlyList<TypedSession> visible,
        DayRange range,
        DayBoundary boundary)
    {
        ILookup<DateOnly, TypedSession> byDay = visible.ToLookup(item => boundary.DayOf(item.Session.EndLocal));

        List<DayTotal> days = [];

        foreach (DateOnly day in range.EnumerateDays())
        {
            List<TypeTotal> byType = byDay[day]
                .GroupBy(item => item.Type, WorkTypes.Comparer)
                .Select(group => new TypeTotal(
                    group.Key,
                    group.Sum(item => (long)item.Session.DurationSec)))
                .OrderByDescending(total => total.Seconds)
                .ToList();

            days.Add(new DayTotal(day, byType.Sum(total => total.Seconds), byType));
        }

        return days;
    }

    private readonly record struct TypedSession(WorkSession Session, string Type);
}
