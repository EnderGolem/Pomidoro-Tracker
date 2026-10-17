using System.Globalization;
using Pomidoro.Domain;

namespace Pomidoro.Formatting;

// Весь показ чисел и дат идёт через InvariantCulture: иначе на машине с другой
// локалью разделитель дробной части и календарь меняются вместе с ней.
internal static class Format
{
    private static readonly CultureInfo Culture = CultureInfo.InvariantCulture;

    // Секунды округляем вверх, чтобы сразу после старта на циферблате было ровно 25:00.
    public static string Clock(TimeSpan remaining)
    {
        TimeSpan rounded = TimeSpan.FromSeconds(Math.Ceiling(remaining.TotalSeconds));

        return rounded.TotalHours >= 1.0
            ? string.Create(Culture, $"{(int)rounded.TotalHours}:{rounded.Minutes:D2}:{rounded.Seconds:D2}")
            : string.Create(Culture, $"{rounded.Minutes:D2}:{rounded.Seconds:D2}");
    }

    public static string HoursNumber(long seconds) =>
        Math.Round(seconds / 3600.0, 1, MidpointRounding.AwayFromZero).ToString("0.0", Culture);

    public static string Minutes(TimeSpan duration) =>
        ((int)Math.Round(duration.TotalMinutes)).ToString(Culture);

    public static string Integer(int value) => value.ToString(Culture);

    public static string TimeOfDay(DateTime local) => local.ToString("HH:mm", Culture);

    public static string DayAndMonth(DateOnly day) => day.ToString("dd.MM", Culture);

    public static string DayNumber(DateOnly day) => day.Day.ToString(Culture);

    public static string Range(DayRange range) => range.IsSingleDay
        ? range.From.ToString("dd.MM.yyyy", Culture)
        : DayAndMonth(range.From) + " — " + range.To.ToString("dd.MM.yyyy", Culture);

    // Подпись на оси: при шаге меньше часа нужны десятые, иначе достаточно целых.
    public static string AxisHours(double hours, double stepHours) =>
        hours.ToString(stepHours < 1.0 ? "0.##" : "0.#", Culture);
}
