# Storage

All data access lives in `Storage/`. From the outside it is three things: the
`ISessionStore` contract, the `AppSettings` wrapper over settings, and a separate
contract for the developer console, `ISqlConsole`.

## Where the database lives

```csharp
public const string DatabaseFileName = "pomodoro.db";

public static string DefaultDatabasePath => Path.Combine(AppContext.BaseDirectory, DatabaseFileName);
```

The file sits **next to the executable**. The application folder can be copied as a whole —
the history moves along with it; a backup is a copy of the file. This is a deliberate choice
in favour of portability: `%APPDATA%` would be "more correct", but then copying the folder
would stop working, and the application is meant to be carried around.

For a single-file publish it matters that `AppContext.BaseDirectory` points to the folder
with the `.exe`, not to the temporary extraction directory.

## The `ISessionStore` contract

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

The interface exists for exactly two reasons: tests substitute an in-memory list
(`FakeSessionStore`), and SQLite can, if needed, be replaced wholesale with something else.

What it does **not** have, and why:

- **`IDisposable`.** The store's lifetime is managed by `App`, which holds the
  concrete `SqliteSessionStore`. A contract for "where to put the history" should know
  nothing about releasing resources.
- **Arbitrary SQL.** A method like `Execute(string sql)` would hard-wire SQL into a contract
  that claims "the implementation can be anything". The console lives in `ISqlConsole` instead.
- **Separate methods for totals.** There used to be `GetTotals`, which computed the sum
  with a query. Now the sum is computed in memory over the already loaded list: for a day
  or a month that is a few dozen records, and one data path instead of two leaves fewer
  places for them to diverge.

## Schema

Created when the database is opened, by `SqliteSchema.Apply`:

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

### `EndUtc` — every query filters on it

Stored as a string in the `"O"` format (`2026-10-16T09:31:12.4521870Z`). Text rather than
a number, for two reasons: it is human-readable in manual queries, and it sorts as is
(the format is lexicographically ordered).

```csharp
private static string FormatUtc(DateTime endUtc) =>
    endUtc.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture);

private static DateTime ParseUtc(string raw)
{
    DateTime parsed = DateTime.Parse(raw, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
    return parsed.Kind == DateTimeKind.Utc ? parsed : parsed.ToUniversalTime();
}
```

`RoundtripKind` sets `Kind = Utc` by itself, because the string ends with `Z`.
It cannot be combined with `AdjustToUniversal` — `DateTime.Parse` rejects that,
so the `Kind` check is a separate line.

### `LocalDate` — a reference column

The local end date as `YYYY-MM-DD`. **The application never queries by it.**
It is filled on insert and exists only for the manual queries from the README:
`WHERE LocalDate LIKE '2026%'` reads better than UTC arithmetic.

Queries must not filter on it: the day boundary is configurable, and a value written
under old settings knows nothing about new ones. The index on it is kept because
manual queries do use it.

### `Type` — a string with a migration

The column was added later than the others, and `CREATE TABLE IF NOT EXISTS` will not add
it to an existing table. So after the schema is created there is a check:

```csharp
if (HasTypeColumn(connection)) { return; }

migrate.CommandText = "ALTER TABLE WorkSessions ADD COLUMN Type TEXT NOT NULL DEFAULT '';";
```

`HasTypeColumn` reads `PRAGMA table_info(WorkSessions)` and looks at the second column —
that is where the field name is. Old databases open and work; existing records get
an empty type.

This is the only migration in the project. If a second one is ever needed, the right move
is to introduce `PRAGMA user_version` and a list of steps rather than pile up checks.

## Connection

```csharp
_connection = new SqliteConnection(new SqliteConnectionStringBuilder
{
    DataSource = databasePath,
    Mode = SqliteOpenMode.ReadWriteCreate,
    Pooling = false
}.ToString());
```

- **One connection for the whole lifetime.** All access comes from the UI thread,
  there is no contention, so neither a pool nor locks are needed.
- **`Pooling = false`.** With a pool, `Dispose` does not close the file but returns the
  connection to the pool — and the database can be neither deleted nor copied right
  after the application exits. The tests found this: they create a database in a temporary
  folder and delete it afterwards.
- **`ReadWriteCreate`.** The database may not exist yet — it is created on first run.
  Missing directories are created as well (`Directory.CreateDirectory`).

There are no transactions: each operation is a single command, and the application never
makes batch changes.

## Queries

All are parameterized; there is no string concatenation in SQL anywhere except the
developer console.

### Insert

```sql
INSERT INTO WorkSessions (EndUtc, LocalDate, DurationSec, Type)
VALUES ($endUtc, $localDate, $durationSec, $type);
```

`LocalDate` is computed right here, from `session.EndLocal`. The domain knows nothing
about this column.

### Selecting a period

```sql
SELECT Id, EndUtc, DurationSec, Type FROM WorkSessions
WHERE EndUtc >= $from AND EndUtc < $to
ORDER BY EndUtc DESC, Id DESC;
```

The bounds come from `DayBoundary`: `$from = boundary.StartUtc(range.From)`,
`$to = boundary.EndUtc(range.To)`. The upper bound is **exclusive**, so a day that
ends exactly on the boundary does not land in both periods at once.

Sorted "newest first"; `Id DESC` as the second key keeps the order stable
if two records share the same time down to the tick.

### Type catalog

```sql
SELECT Name FROM TypeCatalog ORDER BY Id;
```

Ordered by `Id`, i.e. by the time of addition. The order of legend chips and the colours
assigned to types depend on it, so alphabetical order would be worse here:
adding a new type would repaint half the chart.

Adding:

```csharp
if (trimmed.Length == 0 || WorkTypes.IsReservedName(trimmed) || FindCatalogMatches(trimmed).Count > 0)
{
    return;
}
```

Three silent rejections: an empty name, a reserved name ("No type" / «Без типа», "—")
and a case-insensitive duplicate. Silent because this is input into a small field,
not a form with validation; the mistake is obvious from the list not changing.

Removal deletes **all** case-insensitive matches. Records that have this type are left
untouched — it simply stops being offered for new ones.

## Settings

The `Settings` table holds key-value pairs, with the value stored as text.
On top of it sits `AppSettings`, so that string keys and ranges do not get scattered
across the code:

```csharp
public const int MinMinutes = 1;
public const int MaxMinutes = 600;
public const int DefaultWorkMinutes = 25;
public const int DefaultRestMinutes = 5;
```

| Database key | Property | Range |
|---|---|---|
| `WorkMinutes` | `WorkMinutes` | 1…600 |
| `BreakMinutes` | `RestMinutes` | 1…600 |
| `DayBoundaryHour` | `DayBoundaryHour` | 0…23 |

The `BreakMinutes` key is left over from the phase's former name: that is the key existing
databases contain, and renaming it for neatness would cost users the setting.

Values are **read once at startup** and cached in fields, and written immediately
on change. Two consequences follow: reading a setting does not touch the database, and
a crash of the application loses nothing (settings used to be saved when the window closed
and were lost if the exit did not go through the tray menu).

Every save raises the `Changed` event — `MainViewModel` subscribes to it and reloads
the open screen: changing the day boundary changes what counts as "today".

Values from the database are clamped to the valid range on read: if someone wrote `99999`
there through the console, the application shows `600` instead of breaking.

## Developer console

```csharp
internal interface ISqlConsole
{
    string DatabasePath { get; }
    SqlQueryResult Execute(string sql);
}
```

It is implemented by the same `SqliteSessionStore`, but the contract is separate: the main
window gets `ISessionStore`, the console window gets `ISqlConsole`. This is the only place
in the project where SQL comes from outside.

### What happens to a query

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

- The query goes **to the application's live connection** — the same one the screens use.
- Several statements separated by `;` run one after another; `NextResult` collects all sets.
- Statements without a result (`INSERT`, `UPDATE`, `CREATE`) give `FieldCount == 0`
  and do not appear in the output, but they are counted in `RecordsAffected`.
- Any exception is caught and returned in `SqlQueryResult.Error` — the console
  shows the error text instead of a table. `catch (Exception)` here is deliberate;
  the analyzer suppression sits next to the explanation.

Output limits: `MaxSqlRows = 1000` rows per set (beyond that, `Truncated`),
`MaxCellLength = 200` characters per cell. `NULL` is printed as `NULL`,
binary data as `BLOB[n]`, and line breaks inside a cell are replaced with spaces
so the table does not fall apart.

### What the console does not do

- **It does not validate the query.** `DROP TABLE WorkSessions` will run. This is a tool
  for yourself, and a warning about it is shown right in the window.
- **It does not run in the background.** A long query (for example `VACUUM` on a large
  database) freezes the window until it finishes.
- **It does not refresh the settings cache.** An `UPDATE Settings` from the console will not
  be picked up until the application is restarted: `AppSettings` keeps the values in fields.

After every query `App.RefreshAfterSql` asks the main window to reload the database.
If a table the screen depends on did not survive the query, the error is appended
to the console output, and the application stays alive.

### `SqlQueryResult`

```csharp
public bool Failed => Error is not null;
public int RowCount => Sets.Sum(set => set.Rows.Count);
public int ChangedRows => Math.Max(RecordsAffected, 0);
```

`ChangedRows` clips the `-1` that `ExecuteReader` returns for `SELECT`:
there is no point in showing the user "rows changed: -1".

The result is printed by `Formatting/SqlResultText`: it aligns columns to the longest
value, separates multiple sets with headers, and labels an empty result as
"(empty)". It is a pure function and can be tested without a window.
