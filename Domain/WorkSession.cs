namespace Pomidoro.Domain;

// A finished work interval is the only thing that goes into history.
// Breaks and pauses aren't saved: all that matters is when the work ended and how long it took.
internal sealed record WorkSession
{
    public long Id { get; init; }

    public required DateTime EndUtc { get; init; }

    public required int DurationSec { get; init; }

    // An empty string means no type is set. Stored as a string, not a reference to the catalog:
    // a type removed from the catalog must stay in history.
    public string Type { get; init; } = string.Empty;

    public DateTime EndLocal => EndUtc.ToLocalTime();

    public TimeSpan Duration => TimeSpan.FromSeconds(DurationSec);
}
