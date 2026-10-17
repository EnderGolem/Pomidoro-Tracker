namespace Pomidoro.Tests.Fakes;

// Время идёт только тогда, когда его двигает тест.
internal sealed class FakeTime : TimeProvider
{
    private DateTimeOffset _utcNow;

    public FakeTime(DateTimeOffset start)
    {
        _utcNow = start;
    }

    public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;

    public override long TimestampFrequency => TimeSpan.TicksPerSecond;

    public override DateTimeOffset GetUtcNow() => _utcNow;

    public override long GetTimestamp() => _utcNow.UtcTicks;

    public void Advance(TimeSpan delta) => _utcNow = _utcNow.Add(delta);
}
