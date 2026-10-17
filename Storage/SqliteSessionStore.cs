using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using Microsoft.Data.Sqlite;
using Pomidoro.Domain;

namespace Pomidoro.Storage;

// Соединение живёт всё время работы приложения и используется только из UI-потока,
// поэтому ни пула, ни блокировок здесь нет.
internal sealed class SqliteSessionStore : ISessionStore, ISqlConsole, IDisposable
{
    public const string DatabaseFileName = "pomodoro.db";

    private const int MaxSqlRows = 1000;
    private const int MaxCellLength = 200;

    private readonly SqliteConnection _connection;

    public SqliteSessionStore(string databasePath)
    {
        DatabasePath = databasePath;

        string? directory = Path.GetDirectoryName(databasePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        _connection = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            Mode = SqliteOpenMode.ReadWriteCreate,

            // Соединение одно и живёт всё время работы приложения, выигрывать пулу нечего.
            // Зато с ним Dispose не отпускает файл базы, и её нельзя ни удалить, ни скопировать.
            Pooling = false
        }.ToString());

        _connection.Open();
        SqliteSchema.Apply(_connection);
    }

    public static string DefaultDatabasePath => Path.Combine(AppContext.BaseDirectory, DatabaseFileName);

    public string DatabasePath { get; }

    public void Add(WorkSession session)
    {
        using SqliteCommand command = _connection.CreateCommand();
        command.CommandText =
            "INSERT INTO WorkSessions (EndUtc, LocalDate, DurationSec, Type) " +
            "VALUES ($endUtc, $localDate, $durationSec, $type);";
        command.Parameters.AddWithValue("$endUtc", FormatUtc(session.EndUtc));
        command.Parameters.AddWithValue("$localDate", FormatLocalDate(session.EndLocal));
        command.Parameters.AddWithValue("$durationSec", session.DurationSec);
        command.Parameters.AddWithValue("$type", session.Type);
        command.ExecuteNonQuery();
    }

    // Период разворачивается в границы по EndUtc. Колонка LocalDate для выборок не годится:
    // она проставлена при записи и не знает, что границу суток потом подвинули.
    public IReadOnlyList<WorkSession> GetRange(DayRange range, DayBoundary boundary)
    {
        using SqliteCommand command = _connection.CreateCommand();
        command.CommandText =
            "SELECT Id, EndUtc, DurationSec, Type FROM WorkSessions " +
            "WHERE EndUtc >= $from AND EndUtc < $to ORDER BY EndUtc DESC, Id DESC;";
        command.Parameters.AddWithValue("$from", FormatUtc(boundary.StartUtc(range.From)));
        command.Parameters.AddWithValue("$to", FormatUtc(boundary.EndUtc(range.To)));

        List<WorkSession> sessions = [];

        using SqliteDataReader reader = command.ExecuteReader();
        while (reader.Read())
        {
            sessions.Add(new WorkSession
            {
                Id = reader.GetInt64(0),
                EndUtc = ParseUtc(reader.GetString(1)),
                DurationSec = reader.GetInt32(2),
                Type = reader.IsDBNull(3) ? string.Empty : reader.GetString(3)
            });
        }

        return sessions;
    }

    public void UpdateType(long id, string type)
    {
        using SqliteCommand command = _connection.CreateCommand();
        command.CommandText = "UPDATE WorkSessions SET Type = $type WHERE Id = $id;";
        command.Parameters.AddWithValue("$type", type);
        command.Parameters.AddWithValue("$id", id);
        command.ExecuteNonQuery();
    }

    public void UpdateDuration(long id, int durationSec)
    {
        using SqliteCommand command = _connection.CreateCommand();
        command.CommandText = "UPDATE WorkSessions SET DurationSec = $durationSec WHERE Id = $id;";
        command.Parameters.AddWithValue("$durationSec", durationSec);
        command.Parameters.AddWithValue("$id", id);
        command.ExecuteNonQuery();
    }

    public void Delete(long id)
    {
        using SqliteCommand command = _connection.CreateCommand();
        command.CommandText = "DELETE FROM WorkSessions WHERE Id = $id;";
        command.Parameters.AddWithValue("$id", id);
        command.ExecuteNonQuery();
    }

    public IReadOnlyList<string> GetTypeCatalog()
    {
        using SqliteCommand command = _connection.CreateCommand();
        command.CommandText = "SELECT Name FROM TypeCatalog ORDER BY Id;";

        List<string> names = [];

        using SqliteDataReader reader = command.ExecuteReader();
        while (reader.Read())
        {
            names.Add(reader.GetString(0));
        }

        return names;
    }

    // Регистр сравниваем в C#, а не констрейнтом COLLATE NOCASE: встроенный в SQLite
    // NOCASE сворачивает только латиницу A-Z и считает «созвон» и «СОЗВОН» разными словами.
    public void AddTypeToCatalog(string name)
    {
        string trimmed = name.Trim();

        if (trimmed.Length == 0 || WorkTypes.IsReservedName(trimmed) || FindCatalogMatches(trimmed).Count > 0)
        {
            return;
        }

        using SqliteCommand command = _connection.CreateCommand();
        command.CommandText = "INSERT OR IGNORE INTO TypeCatalog (Name) VALUES ($name);";
        command.Parameters.AddWithValue("$name", trimmed);
        command.ExecuteNonQuery();
    }

    public void RemoveTypeFromCatalog(string name)
    {
        foreach (string stored in FindCatalogMatches(name))
        {
            using SqliteCommand command = _connection.CreateCommand();
            command.CommandText = "DELETE FROM TypeCatalog WHERE Name = $name;";
            command.Parameters.AddWithValue("$name", stored);
            command.ExecuteNonQuery();
        }
    }

    public int GetSetting(string key, int fallback)
    {
        using SqliteCommand command = _connection.CreateCommand();
        command.CommandText = "SELECT Value FROM Settings WHERE Key = $key;";
        command.Parameters.AddWithValue("$key", key);

        return command.ExecuteScalar() is string text &&
               int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int value)
            ? value
            : fallback;
    }

    public void SetSetting(string key, int value)
    {
        using SqliteCommand command = _connection.CreateCommand();
        command.CommandText =
            "INSERT INTO Settings (Key, Value) VALUES ($key, $value) " +
            "ON CONFLICT(Key) DO UPDATE SET Value = excluded.Value;";
        command.Parameters.AddWithValue("$key", key);
        command.Parameters.AddWithValue("$value", value.ToString(CultureInfo.InvariantCulture));
        command.ExecuteNonQuery();
    }

    [SuppressMessage(
        "Security",
        "CA2100:Review SQL queries for security vulnerabilities",
        Justification = "Консоль разработчика существует ровно для того, чтобы выполнить введённый текст как есть")]
    [SuppressMessage(
        "Design",
        "CA1031:Do not catch general exception types",
        Justification = "Любая ошибка запроса должна попасть в окно консоли, а не уронить приложение")]
    public SqlQueryResult Execute(string sql)
    {
        Stopwatch watch = Stopwatch.StartNew();
        List<SqlResultSet> sets = [];

        try
        {
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

            watch.Stop();

            return new SqlQueryResult
            {
                Sets = sets,
                RecordsAffected = reader.RecordsAffected,
                ElapsedMs = watch.ElapsedMilliseconds
            };
        }
        catch (Exception exception)
        {
            watch.Stop();

            return new SqlQueryResult
            {
                Sets = sets,
                RecordsAffected = 0,
                ElapsedMs = watch.ElapsedMilliseconds,
                Error = $"{exception.GetType().Name}: {exception.Message}"
            };
        }
    }

    public void Dispose()
    {
        _connection.Dispose();
    }

    private List<string> FindCatalogMatches(string name) =>
        GetTypeCatalog()
            .Where(stored => WorkTypes.Comparer.Equals(stored, name))
            .ToList();

    private static SqlResultSet ReadSet(SqliteDataReader reader)
    {
        List<string> columns = [];
        for (int i = 0; i < reader.FieldCount; i++)
        {
            columns.Add(reader.GetName(i));
        }

        List<IReadOnlyList<string>> rows = [];
        bool truncated = false;

        while (reader.Read())
        {
            if (rows.Count >= MaxSqlRows)
            {
                truncated = true;
                break;
            }

            List<string> cells = [];
            for (int i = 0; i < reader.FieldCount; i++)
            {
                cells.Add(DescribeCell(reader, i));
            }

            rows.Add(cells);
        }

        return new SqlResultSet
        {
            Columns = columns,
            Rows = rows,
            Truncated = truncated
        };
    }

    private static string DescribeCell(SqliteDataReader reader, int index)
    {
        if (reader.IsDBNull(index))
        {
            return "NULL";
        }

        object value = reader.GetValue(index);

        if (value is byte[] bytes)
        {
            return $"BLOB[{bytes.Length}]";
        }

        string text = (Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty)
            .Replace("\r", " ", StringComparison.Ordinal)
            .Replace("\n", " ", StringComparison.Ordinal);

        return text.Length > MaxCellLength
            ? string.Concat(text.AsSpan(0, MaxCellLength), "…")
            : text;
    }

    private static string FormatUtc(DateTime endUtc) =>
        endUtc.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture);

    // Справочная колонка для ручных запросов из README: выборки идут не по ней.
    private static string FormatLocalDate(DateTime endLocal) =>
        endLocal.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    // Формат "O" заканчивается на Z, поэтому RoundtripKind сам выставляет Kind = Utc;
    // комбинировать его с AdjustToUniversal нельзя — DateTime.Parse на это ругается.
    private static DateTime ParseUtc(string raw)
    {
        DateTime parsed = DateTime.Parse(raw, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);

        return parsed.Kind == DateTimeKind.Utc ? parsed : parsed.ToUniversalTime();
    }
}
