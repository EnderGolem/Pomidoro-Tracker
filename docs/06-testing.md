# Тесты

```
dotnet test tests\Pomidoro.Tests\Pomidoro.Tests.csproj -c DebugLocal
```

Сейчас 162 теста, проходят за полторы секунды. Ни один не ждёт настоящего времени,
не трогает пользовательскую базу и не открывает окон.

## Что покрыто

| Файл | Тестов | О чём |
|---|---|---|
| `PomodoroTimerTests` | 11 | полный цикл, паузы, досрочная остановка, границы |
| `DayBoundaryTests` | 9 | логический день, перенос ночной работы, зажим часа |
| `StatsPeriodsTests` | 6 | недели с понедельника, месяцы, перечисление дней |
| `WorkTypesTests` | 14 | приведение регистра, пустой тип, служебные имена обоих языков |
| `StatsCalculatorTests` | 10 | итоги, дни, порядок типов, медиана, регистр |
| `ChartLayoutTests` | 18 | шаг сетки, потолок оси, ширины, высоты |
| `FormatTests` | 15 | циферблат, числа, даты, независимость от локали |
| `StringsTests` | 24 | полнота обоих языков, склонения, ключи разметки |
| `SqliteSessionStoreTests` | 23 | схема, выборки, правки, справочник, настройки, консоль |
| `SessionRowViewModelTests` | 15 | правка минут и типа, откат мусора, отсутствие лишних записей |
| `StatsViewModelTests` | 14 | легенда, период, диаграмма, правка и удаление строк |
| `MainViewModelTests` | 3 | сборка экранов, запись помидорки, закрытие меню |

Покрыты все слои, кроме разметки: домен, счёт статистики, геометрия, форматирование,
хранилище и модели представления.

## Чего нет

**Тестов разметки.** Проверять привязки автоматически можно, но для приложения
такого размера это дороже, чем посмотреть глазами. Поэтому окна проверяются вручную
(и через скрипты UI Automation, когда нужно убедиться, что ничего не отвалилось).

**Отдельных тестов `TimerViewModel`.** Его собственная логика — пересказ домена
в строки для экрана, а всё остальное проверяется со стороны `MainViewModelTests`:
помидорка, запущенная командой и доведённая тикером до конца, доходит до хранилища.

**Тестов звука и трея.** Там нечего проверять без железа.

## Подделки

Всё в `tests/Pomidoro.Tests/Fakes/`.

### `FakeTime`

```csharp
internal sealed class FakeTime : TimeProvider
{
    public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;
    public override long TimestampFrequency => TimeSpan.TicksPerSecond;
    public override DateTimeOffset GetUtcNow() => _utcNow;
    public override long GetTimestamp() => _utcNow.UtcTicks;

    public void Advance(TimeSpan delta) => _utcNow = _utcNow.Add(delta);
}
```

И настенные часы, и монотонный счётчик идут от одного поля, поэтому `Advance`
двигает сразу всё. Часовой пояс — UTC, чтобы «сейчас» в тестах было предсказуемым.

### `FakeTicker`

Хранит, запущен ли он, и умеет `Fire()` — дёрнуть `Tick` вручную.

### `FakeSessionStore`

`ISessionStore` поверх трёх списков в памяти. Повторяет те правила хранилища,
которые видны снаружи: присваивает `Id`, фильтрует по логическому дню, отбрасывает
дубликаты типов без учёта регистра.

Для проверок он ещё и запоминает, что с ним делали:

```csharp
public List<string> UpdatedTypes { get; } = [];
public List<long> DeletedIds { get; } = [];
```

Отсюда берётся, например, тест «показ строки ничего не пишет в базу»:
после создания трёх строк `UpdatedTypes` обязан быть пустым.

### `TestSessions`

```csharp
public static WorkSession At(DateTime local, int minutes, string type = "") => new()
{
    EndUtc = local.ToUniversalTime(),
    DurationSec = minutes * 60,
    Type = type
};
```

Сессии задаются **локальным** временем и переводятся в UTC. Это важно: `WorkSession.EndLocal`
считается по часовому поясу машины, и если задавать время сразу в UTC, тесты проходили бы
в Москве и падали бы в Лондоне.

## Как написано

### Имена

Имя теста — фраза на английском через подчёркивания, которая читается как утверждение:

```
Paused_time_does_not_count
Same_type_written_in_different_case_stays_one_type
Garbage_rolls_back_to_the_previous_value
```

В отчёте о прогоне такой список читается как описание поведения. Анализатор на имена
с подчёркиваниями (CA1707) выключен для папки `tests/**` в `.editorconfig`.

### Один тест — одно утверждение о поведении

```csharp
[Fact]
public void Finished_work_is_recorded_with_its_planned_length_and_rest_starts()
{
    _timer.Start(Work, Rest);

    // Тик пришёл позже нуля, но в историю должно попасть ровно 25 минут.
    _time.Advance(Work + TimeSpan.FromMilliseconds(80));
    TimerStep step = _timer.Advance();

    Assert.Equal(TimerEvent.WorkFinished, step.Event);
    Assert.Equal(25 * 60, step.Completed.DurationSec);
    Assert.Equal(Rest, _timer.PhaseLength);
}
```

Несколько `Assert` в одном тесте допустимы, если они про одно и то же событие.

### Таблицы вместо копипасты

```csharp
[Theory]
[InlineData(0, "ни одной помидорки")]
[InlineData(1, "1 помидорка")]
[InlineData(3, "3 помидорки")]
[InlineData(5, "5 помидорок")]
[InlineData(11, "11 помидорок")]
[InlineData(21, "21 помидорка")]
[InlineData(114, "114 помидорок")]
public void Pomodoros_follow_russian_rules(int count, string expected)
```

### Тесты хранилища идут против настоящего SQLite

```csharp
_path = Path.Combine(Path.GetTempPath(), "pomidoro-tests", Guid.NewGuid().ToString("N"), "test.db");
_store = new SqliteSessionStore(_path);
```

Каждый тест получает свою базу во временной папке и удаляет её в `Dispose`.
Подделывать базу здесь было бы бессмысленно: проверяются как раз схема, формат дат,
миграция и SQL — то, что в подделке пришлось бы выдумать заново.

Именно эти тесты нашли, что соединение остаётся в пуле и не отпускает файл:
удаление папки падало с «файл занят другим процессом». Лечится `Pooling=false`.

### Проверки, независимые от среды

Там, где поведение зависит от часового пояса, проверяется не конкретное значение,
а инвариант:

```csharp
DateTime start = boundary.StartUtc(day).ToLocalTime();
DateTime end = boundary.EndUtc(day).ToLocalTime().AddSeconds(-1);

Assert.Equal(day, boundary.DayOf(start));
Assert.Equal(day, boundary.DayOf(end));
```

А независимость форматирования от локали проверяется прямо — подменой культуры:

```csharp
CultureInfo.CurrentCulture = new CultureInfo("ru-RU");
Assert.Equal("18.6 часа", Format.Hours(66967));
```

## Как добавить тест

1. Файл кладётся в `tests/Pomidoro.Tests/`, имя — `ИмяТипаTests.cs`.
2. Класс `public`, методы помечены `[Fact]` или `[Theory]`.
3. Внутренние типы приложения видны благодаря `InternalsVisibleTo` в `Pomidoro.csproj` —
   ничего дополнительно подключать не нужно.
4. Если нужны время, тикер или хранилище — бери подделки из `Fakes/`, не настоящие.
5. Если проверяешь что-то, что раньше было сломано, напиши в комментарии, что именно:
   так тест не удалят через год как «странный».

Пример полного теста модели представления:

```csharp
[Fact]
public void Click_leaves_only_the_type_that_was_clicked()
{
    _stats.ToggleType("Работа", shiftHeld: false);

    Assert.Equal([true, false, false], Shown());
    Assert.Equal("1.0 часа", _stats.HoursText);
    Assert.Single(_stats.List.Rows);
}
```

Проверяется сразу всё, что должен увидеть человек: состояние плашек, сумма часов
и количество строк в списке.
