using Microsoft.Data.Sqlite;

namespace Pomidoro.Storage;

internal static class SqliteSchema
{
    public static void Apply(SqliteConnection connection)
    {
        using (SqliteCommand create = connection.CreateCommand())
        {
            create.CommandText =
                """
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
                """;
            create.ExecuteNonQuery();
        }

        // The Type column appeared later than the others, and CREATE TABLE IF NOT EXISTS
        // won't add it to an already existing table.
        if (HasTypeColumn(connection))
        {
            return;
        }

        using SqliteCommand migrate = connection.CreateCommand();
        migrate.CommandText = "ALTER TABLE WorkSessions ADD COLUMN Type TEXT NOT NULL DEFAULT '';";
        migrate.ExecuteNonQuery();
    }

    private static bool HasTypeColumn(SqliteConnection connection)
    {
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "PRAGMA table_info(WorkSessions);";

        using SqliteDataReader reader = command.ExecuteReader();

        while (reader.Read())
        {
            // The second column of PRAGMA table_info holds the column name.
            if (string.Equals(reader.GetString(1), "Type", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
