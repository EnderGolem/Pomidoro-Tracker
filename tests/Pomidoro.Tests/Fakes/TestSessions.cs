using Pomidoro.Domain;

namespace Pomidoro.Tests.Fakes;

internal static class TestSessions
{
    // Сессии задаются локальным временем: WorkSession.EndLocal считается по часовому поясу
    // машины, и без этого тесты зависели бы от того, где их запускают.
    public static WorkSession At(DateTime local, int minutes, string type = "") => new()
    {
        EndUtc = local.ToUniversalTime(),
        DurationSec = minutes * 60,
        Type = type
    };

    public static WorkSession At(int year, int month, int day, int hour, int minutes, string type = "") =>
        At(new DateTime(year, month, day, hour, 0, 0, DateTimeKind.Local), minutes, type);
}
