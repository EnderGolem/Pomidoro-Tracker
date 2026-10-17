# Хранилище

Вся работа с данными живёт в `Storage/`. Снаружи это три вещи: контракт
`ISessionStore`, обёртка над настройками `AppSettings` и отдельный контракт
консоли разработчика `ISqlConsole`.

## Где лежит база

```csharp
public const string DatabaseFileName = "pomodoro.db";

public static string DefaultDatabasePath => Path.Combine(AppContext.BaseDirectory, DatabaseFileName);
```

Файл лежит **рядом с исполняемым файлом**. Папку приложения можно скопировать целиком —
история переедет вместе с ней; бэкап = копия файла. Это сознательный выбор в пользу
переносимости: `%APPDATA%` был бы «правильнее», но тогда копирование папки перестало бы
работать, а приложение рассчитано на то, что его носят с собой.

Для single-file-публикации важно, что `AppContext.BaseDirectory` указывает на папку с
`.exe`, а не на временный каталог распаковки.

## Контракт `ISessionStore`

```csharp
internal interface ISessionStore
{
    void Add(WorkSession session);
    IReadOnlyList<WorkSession> GetRange(DayRange range, DayBoundary boundary);
    void UpdateType(long id, string type);
    void UpdateDuration(long id, int durationSec);
    void Delete(long id);

    IReadOnlyList<string> GetTypeCatalog();
    void AddTypeToCatalog(string name);
    void RemoveTypeFromCatalog(string name);

    int GetSetting(string key, int fallback);
    void SetSetting(string key, int value);
}
```

Интерфейс нужен ровно для двух вещей: тесты подставляют список в памяти
(`FakeSessionStore`), а SQLite при желании меняется целиком на что-то другое.

Чего в нём **нет** и почему:

- **`IDisposable`.** Временем жизни хранилища распоряжается `App`, который держит
  конкретный `SqliteSessionStore`. Контракт «куда класть историю» ничего не должен
  знать про освобождение ресурсов.
- **Произвольного SQL.** Метод вида `Execute(string sql)` намертво вшил бы SQL в контракт,
  который заявлен как «реализация может быть любой». Консоль вынесена в `ISqlConsole`.
- **Отдельных методов для итогов.** Раньше был `GetTotals`, считавший сумму запросом.
  Сейчас сумма считается в памяти по уже загруженному списку: за день или месяц это
  десятки записей, а один путь данных вместо двух — меньше мест, где они разойдутся.

## Схема

Создаётся при открытии базы, `SqliteSchema.Apply`:

```sql
CREATE TABLE IF NOT EXISTS WorkSessions (
    Id          INTEGER PRIMARY KEY AUTOINCREMENT,
    EndUtc      TEXT    NOT NULL,
    LocalDate   TEXT    NOT NULL,
    DurationSec INTEGER NOT NULL,
    Type        TEXT    NOT NULL DEFAULT '');

CREATE INDEX IF NOT EXISTS IX_WorkSessions_EndUtc ON WorkSessions (EndUtc);
CREATE INDEX IF NOT EXISTS IX_WorkSessions_LocalDate ON WorkSessions (LocalDate);

CREATE TABLE IF NOT EXISTS Settings (
    Key   TEXT PRIMARY KEY,
    Value TEXT NOT NULL);

CREATE TABLE IF NOT EXISTS TypeCatalog (
    Id   INTEGER PRIMARY KEY AUTOINCREMENT,
    Name TEXT NOT NULL UNIQUE);
```

### `EndUtc` — по нему идут все выборки

Хранится строкой в формате `"O"` (`2026-10-16T09:31:12.4521870Z`). Текст, а не число,
по двум причинам: видно глазами при ручных запросах и сортируется как есть
(формат лексикографически упорядочен).

```csharp
private static string FormatUtc(DateTime endUtc) =>
    endUtc.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture);

private static DateTime ParseUtc(string raw)
{
    DateTime parsed = DateTime.Parse(raw, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
    return parsed.Kind == DateTimeKind.Utc ? parsed : parsed.ToUniversalTime();
}
```

`RoundtripKind` сам выставляет `Kind = Utc`, потому что строка кончается на `Z`.
Комбинировать его с `AdjustToUniversal` нельзя — `DateTime.Parse` на это ругается,
поэтому проверка `Kind` стоит отдельной строкой.

### `LocalDate` — справочная колонка

Локальная дата окончания в виде `ГГГГ-ММ-ДД`. **Приложение по ней ничего не выбирает.**
Она заполняется при вставке и существует только ради ручных запросов из README:
`WHERE LocalDate LIKE '2026%'` читается лучше, чем арифметика по UTC.

Выборки по ней вести нельзя: граница суток настраивается, и значение, записанное
при старых настройках, не знает о новых. Индекс на ней оставлен, потому что ручные
запросы по ней всё-таки делают.

### `Type` — строка с миграцией

Колонка появилась позже остальных, а `CREATE TABLE IF NOT EXISTS` в существующую
таблицу её не добавит. Поэтому после создания схемы идёт проверка:

```csharp
if (HasTypeColumn(connection)) { return; }

migrate.CommandText = "ALTER TABLE WorkSessions ADD COLUMN Type TEXT NOT NULL DEFAULT '';";
```

`HasTypeColumn` читает `PRAGMA table_info(WorkSessions)` и смотрит вторую колонку —
там лежит имя поля. Старые базы открываются и работают, существующие записи получают
пустой тип.

Это единственная миграция в проекте. Если понадобится вторая, правильнее завести
`PRAGMA user_version` и список шагов, а не наращивать проверки.

## Соединение

```csharp
_connection = new SqliteConnection(new SqliteConnectionStringBuilder
{
    DataSource = databasePath,
    Mode = SqliteOpenMode.ReadWriteCreate,
    Pooling = false
}.ToString());
```

- **Одно соединение на всё время работы.** Обращения идут только из UI-потока,
  конкуренции нет, поэтому ни пула, ни блокировок не нужно.
- **`Pooling = false`.** С пулом `Dispose` не закрывает файл, а возвращает соединение
  в пул — и базу нельзя ни удалить, ни скопировать сразу после закрытия приложения.
  Нашли это тесты: они создают базу во временной папке и удаляют её за собой.
- **`ReadWriteCreate`.** Базы может ещё не быть — при первом запуске она создаётся.
  Недостающие каталоги создаются тоже (`Directory.CreateDirectory`).

Транзакций нет: каждая операция — одна команда, а пакетных изменений в приложении
не бывает.

## Запросы

Все параметризованы, конкатенации в SQL нет нигде, кроме консоли разработчика.

### Вставка

```sql
INSERT INTO WorkSessions (EndUtc, LocalDate, DurationSec, Type)
VALUES ($endUtc, $localDate, $durationSec, $type);
```

`LocalDate` считается здесь же, из `session.EndLocal`. Домен про эту колонку не знает.

### Выборка за период

```sql
SELECT Id, EndUtc, DurationSec, Type FROM WorkSessions
WHERE EndUtc >= $from AND EndUtc < $to
ORDER BY EndUtc DESC, Id DESC;
```

Границы считает `DayBoundary`: `$from = boundary.StartUtc(range.From)`,
`$to = boundary.EndUtc(range.To)`. Правый конец **строгий**, поэтому день,
заканчивающийся ровно на границе, не попадает в оба периода сразу.

Сортировка «свежие сверху»; `Id DESC` вторым ключом, чтобы порядок был устойчивым,
если две записи совпали по времени до тика.

### Справочник типов

```sql
SELECT Name FROM TypeCatalog ORDER BY Id;
```

Порядок — по `Id`, то есть по времени добавления. От него зависит порядок плашек
в легенде и закреплённые за типами цвета, поэтому алфавит здесь был бы хуже:
добавление нового типа перекрасило бы половину диаграммы.

Добавление:

```csharp
if (trimmed.Length == 0 || WorkTypes.IsReservedName(trimmed) || FindCatalogMatches(trimmed).Count > 0)
{
    return;
}
```

Три отказа молча: пустое имя, служебное имя («Без типа», «—») и дубликат без учёта
регистра. Молча — потому что это ввод в маленькое поле, а не форма с валидацией;
ошибку видно сразу по тому, что список не изменился.

Удаление убирает **все** совпадения без учёта регистра. Записи, которым этот тип
проставлен, не трогаются — он просто перестаёт предлагаться для новых.

## Настройки

Таблица `Settings` — пары ключ-значение, значение хранится текстом.
Поверх неё лежит `AppSettings`, чтобы по коду не разъезжались строковые ключи
и диапазоны:

```csharp
public const int MinMinutes = 1;
public const int MaxMinutes = 600;
public const int DefaultWorkMinutes = 25;
public const int DefaultRestMinutes = 5;
```

| Ключ в базе | Свойство | Диапазон |
|---|---|---|
| `WorkMinutes` | `WorkMinutes` | 1…600 |
| `BreakMinutes` | `RestMinutes` | 1…600 |
| `DayBoundaryHour` | `DayBoundaryHour` | 0…23 |

Ключ `BreakMinutes` остался от прежнего названия фазы: в уже существующих базах лежит
именно он, и переименование ради красоты стоило бы потери настройки.

Значения **читаются один раз при запуске** и кэшируются в полях, а пишутся сразу
при изменении. Отсюда два следствия: чтение настройки не ходит в базу, а аварийное
завершение приложения ничего не теряет (раньше настройки сохранялись при закрытии окна
и терялись, если выход шёл не через меню трея).

Любое сохранение поднимает событие `Changed` — на него подписан `MainViewModel`
и перечитывает открытый экран: смена границы суток меняет то, что считается «сегодня».

Значения из базы приводятся в допустимый диапазон при чтении: если туда через консоль
вписали `99999`, приложение покажет `600`, а не сломается.

## Консоль разработчика

```csharp
internal interface ISqlConsole
{
    string DatabasePath { get; }
    SqlQueryResult Execute(string sql);
}
```

Реализует его тот же `SqliteSessionStore`, но контракт отдельный: главное окно
получает `ISessionStore`, окно консоли — `ISqlConsole`. Это единственное место
в проекте, где SQL приходит снаружи.

### Что происходит с запросом

```csharp
using SqliteCommand command = _connection.CreateCommand();
command.CommandText = sql;
using SqliteDataReader reader = command.ExecuteReader();

do
{
    if (reader.FieldCount > 0)
    {
        sets.Add(ReadSet(reader));
    }
}
while (reader.NextResult());
```

- Запрос идёт **в живое соединение приложения** — то же самое, с которым работают экраны.
- Несколько операторов через `;` выполняются подряд, `NextResult` собирает все наборы.
- Операторы без результата (`INSERT`, `UPDATE`, `CREATE`) дают `FieldCount == 0`
  и в вывод не попадают, но считаются в `RecordsAffected`.
- Любое исключение ловится и возвращается в `SqlQueryResult.Error` — консоль
  показывает текст ошибки вместо таблицы. `catch (Exception)` здесь осознанный,
  подавление анализатора стоит рядом с объяснением.

Ограничения вывода: `MaxSqlRows = 1000` строк на набор (дальше `Truncated`),
`MaxCellLength = 200` символов на ячейку. `NULL` печатается как `NULL`,
двоичное — как `BLOB[n]`, переводы строк в ячейке заменяются пробелами,
чтобы таблица не разъехалась.

### Чего консоль не делает

- **Не проверяет запрос.** `DROP TABLE WorkSessions` выполнится. Это инструмент
  для себя, и предупреждение об этом висит прямо в окне.
- **Не выполняется в фоне.** Долгий запрос (например `VACUUM` на большой базе)
  подвесит окно до своего конца.
- **Не обновляет кэш настроек.** `UPDATE Settings` через консоль не подхватится,
  пока приложение не перезапустят: `AppSettings` держит значения в полях.

После каждого запроса `App.RefreshAfterSql` просит главное окно перечитать базу.
Если запроса не пережила таблица, на которой держится экран, ошибка допишется
в вывод консоли, а приложение останется живым.

### `SqlQueryResult`

```csharp
public bool Failed => Error is not null;
public int RowCount => Sets.Sum(set => set.Rows.Count);
public int ChangedRows => Math.Max(RecordsAffected, 0);
```

`ChangedRows` обрезает `-1`, который `ExecuteReader` возвращает для `SELECT`:
показывать пользователю «изменено строк: -1» незачем.

Печатает результат `Formatting/SqlResultText`: выравнивает колонки по самому длинному
значению, разделяет несколько наборов заголовками, пустой результат подписывает как
`(пусто)`. Это чистая функция, её можно проверить тестом без окна.
