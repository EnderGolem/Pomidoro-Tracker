namespace Pomidoro.Mvvm;

// Источник тиков для таймера. Отдельно от него, чтобы ход времени в тестах
// задавался вручную, а не ожиданием настоящих секунд.
internal interface ITicker
{
    event EventHandler? Tick;

    void Start();

    void Stop();
}
