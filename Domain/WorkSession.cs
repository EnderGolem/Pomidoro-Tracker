namespace Pomidoro.Domain;

// Завершённый рабочий промежуток — единственное, что попадает в историю.
// Перерывы и паузы не сохраняются: важно только когда работа закончилась и сколько длилась.
internal sealed record WorkSession
{
    public long Id { get; init; }

    public required DateTime EndUtc { get; init; }

    public required int DurationSec { get; init; }

    // Пустая строка — тип не проставлен. Хранится строкой, а не ссылкой на справочник:
    // убранный из справочника тип должен оставаться в истории.
    public string Type { get; init; } = string.Empty;

    public DateTime EndLocal => EndUtc.ToLocalTime();

    public TimeSpan Duration => TimeSpan.FromSeconds(DurationSec);
}
