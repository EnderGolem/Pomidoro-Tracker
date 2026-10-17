using Pomidoro.Domain;

namespace Pomidoro.Tests.Fakes;

internal static class TestSessions
{
    // Sessions are given in local time: WorkSession.EndLocal is computed from the machine's
    // time zone, and without this the tests would depend on where they run.
    public static WorkSession At(DateTime local, int minutes, string type = "") => new()
    {
        EndUtc = local.ToUniversalTime(),
        DurationSec = minutes * 60,
        Type = type
    };

    public static WorkSession At(int year, int month, int day, int hour, int minutes, string type = "") =>
        At(new DateTime(year, month, day, hour, 0, 0, DateTimeKind.Local), minutes, type);
}
