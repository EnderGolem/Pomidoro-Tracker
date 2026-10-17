namespace Pomidoro.Storage;

// The developer console is kept out of ISessionStore: arbitrary SQL is a property
// of the specific SQLite implementation, not part of the "where history lives" contract.
internal interface ISqlConsole
{
    string DatabasePath { get; }

    SqlQueryResult Execute(string sql);
}
