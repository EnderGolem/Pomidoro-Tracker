namespace Pomidoro.Domain;

internal readonly record struct DayBoundary
{
    public const int MinHour = 0;
    public const int MaxHour = 23;
    public const int DefaultHour = 0;

    public DayBoundary(int hour)
    {
        Hour = Math.Clamp(hour, MinHour, MaxHour);
    }

    public int Hour { get; }

    public static DayBoundary Default => new(DefaultHour);

    public DateOnly DayOf(DateTime local) => DateOnly.FromDateTime(local.AddHours(-Hour));

    public DateOnly Today(TimeProvider time) => DayOf(time.GetLocalNow().DateTime);

    public DateTime StartUtc(DateOnly day) =>
        DateTime.SpecifyKind(day.ToDateTime(TimeOnly.MinValue).AddHours(Hour), DateTimeKind.Unspecified)
            .ToUniversalTime();

    public DateTime EndUtc(DateOnly day) => StartUtc(day.AddDays(1));
}
